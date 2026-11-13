using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.ModLoader;

namespace Augments
{
	/// <summary>
	/// Fully procedural "view from the Bunker" backdrop. No image assets are used:
	/// the planet surface, clouds, nebula and moon are generated with 3D Perlin noise,
	/// and the planet is re-shaded on the CPU every other frame so it slowly rotates.
	///
	/// Lore note: no city lights on the night side — Earth in NieR:Automata is a
	/// machine-overrun, overgrown ruin. The night side only shows faint moonlight and
	/// occasional lightning inside storm systems.
	/// </summary>
	public static class OrbitalBackdrop
	{
		// ───────────────────────── Tunables ─────────────────────────
		private const int MapW = 1024;               // equirectangular surface map (power of two)
		private const int MapH = 512;
		private const float HorizonTop = 0.62f;      // horizon height at screen centre (fraction of screen height)
		private const float RadiusFactor = 1.05f;    // planet radius as fraction of screen width
		private const float AtmThickness = 0.026f;   // atmosphere glow thickness, in planet radii
		private const float LandSpin = 2.4f;         // map columns per second (smooth, synced orbital rotation)
		private const float CloudSpin = 3.0f;        // clouds drift slightly faster than the ground
		private const int MoonSize = 160;
		private const int NebW = 480, NebH = 270;

		private static readonly Vector3 SunDir = Vector3.Normalize(new Vector3(-0.85f, -0.30f, -0.43f));
		private static readonly Vector3 Axis = Vector3.Normalize(new Vector3(0f, -0.55f, 0.84f));
		private static readonly Vector3 AxisX = Vector3.UnitX;
		private static readonly Vector3 AxisZ = Vector3.Cross(Vector3.UnitX, Vector3.Normalize(new Vector3(0f, -0.55f, 0.84f)));

		// ───────────────────────── Generated data ─────────────────────────
		private static volatile bool dataReady;
		private static bool dataStarted;
		private static bool texturesBuilt;
		private static float[] landR, landG, landB, cloudMap;
		private static Color[] nebulaData;

		private const int MoonMapW = 512;
		private const int MoonMapH = 256;
		private static float[] moonAlbedo;
		private static Color[] moonBuffer;
		private static int moonPixelCount;
		private static int[] mMoonIndex, mMoonRowOff, mMoonBaseCol;
		private static float[] mMoonLightR, mMoonLightG, mMoonLightB;
		private static byte[] mMoonAlpha;

		private static Texture2D glowTex, gradientTex, nebulaTex, moonTex, planetTex;
		private static Texture2D emilTex, flightUnitTex, podTex;
		private static Asset<Texture2D> bunkerTexture;

		// ───────────────────────── Planet geometry cache ─────────────────────────
		private static int cachedW = -1, cachedH = -1;
		private static int bufW, bufH, bufDiv, bufTop;
		private static float gCx, gCy, gR;
		private static Color[] buffer;
		private static int planetCount;
		private static int[] pIndex, pRowOff;
		private static float[] pLon, pKr, pKg, pKb, pAr, pAg, pAb;
		private static readonly List<Vector2> nightSpots = new List<Vector2>();

		// ───────────────────────── Animation state ─────────────────────────
		private static readonly Stopwatch clock = Stopwatch.StartNew();
		private static double lastTime;
		private static float totalTime;
		private static float landRot, cloudRot;
		private static float planetFade;
		private static readonly Random fxRand = new Random(2027);

		private struct Star
		{
			public Vector2 Pos;      // normalised [0,1]
			public Vector2 Drift;    // normalised units / second
			public float BaseAlpha, TwinkleSpeed, Phase, Phase2;
			public int Size;
			public bool Spikes;
			public Color Col;
		}
		private static Star[] stars;

		private struct WarFlash
		{
			public bool Active;
			public Vector2 Pos;
			public float Life;
			public float MaxLife;
			public float BaseSize;
			public int Type; // 0 = Plasma Detonation, 1 = Crimson Machine Laser Strike, 2 = Warzone Embers, 3 = EMP Shock Discharge
			public Color PrimaryCol;
			public Color CoreCol;
			public float Angle;
		}

		private const int MaxWarFlashes = 24;
		private static readonly WarFlash[] warFlashes = new WarFlash[MaxWarFlashes];
		private static float warSpawnTimer = 1.6f;
		private static Vector2 lastWarPos;

		private static float shootTimer = 1.5f, shootLife, shootMax;
		private static Vector2 shootPos, shootVel;

		private struct EasterEgg
		{
			public bool Active;
			public int Type; // 0 = Emil's Head, 1 = YoRHa Flight Unit, 2 = Pod 042
			public Vector2 Pos;
			public Vector2 Vel;
			public float Life;
			public float MaxLife;
			public float Rotation;
			public float RotSpeed;
			public float Scale;
			public float Alpha;
		}

		private static EasterEgg egg;
		private static float eggSpawnTimer = 3.5f; // Quick 3.5s initial timer for immediate sighting, then 60s-120s

		// ═════════════════════════════════════════════════════════════
		//  Public API
		// ═════════════════════════════════════════════════════════════

		public static void Draw(SpriteBatch sb, Texture2D pixel, int w, int h)
		{
			float dt = AdvanceClock();
			EnsureBasics();

			if (!dataStarted)
			{
				dataStarted = true;
				Task.Run(GenerateData);
			}
			if (dataReady && !texturesBuilt)
				BuildDataTextures();

			if (w != cachedW || h != cachedH)
				BuildGeometry(w, h);

			// 1. Deep-space gradient (slight airglow towards the horizon)
			sb.Draw(gradientTex, new Rectangle(0, 0, w, h), Color.White);

			// 2. Milky-way / nebula band
			if (nebulaTex != null)
				sb.Draw(nebulaTex, new Rectangle(0, 0, w, h), Color.White * 0.9f);

			// 3. Stars
			DrawStars(sb, pixel, w, h, dt);

			// 3.5 Far-away drifting lights
			DrawDistantLights(sb, pixel, w, h, dt);

			// 4. Moon (the Human Council's server lives up there...)
			DrawMoon(sb, w, h);

			// 5. Planet
			if (texturesBuilt)
			{
				planetFade = Math.Min(1f, planetFade + dt * 0.6f);
				landRot += dt * LandSpin;
				cloudRot += dt * CloudSpin;
				if (landRot > MapW) landRot -= MapW;
				if (cloudRot > MapW) cloudRot -= MapW;

				UpdatePlanet();
				UpdateMoon();

				sb.Draw(planetTex, new Rectangle(0, bufTop, bufW * bufDiv, bufH * bufDiv), Color.White * planetFade);

				DrawWarDestruction(sb, h, dt);
			}

			// 6. Atmospheric asteroid / bolide re-entry (close to Earth, burning in atmosphere)
			DrawShootingStar(sb, pixel, w, h, dt);

			// 7. Sunrise on the limb + lens flare
			DrawSun(sb, w, h);

			// 8. YoRHa Orbital Satellite Base // "The Bunker"
			DrawBunker(sb, w, h);
		}

		public static void Unload()
		{
			Texture2D[] all = { glowTex, gradientTex, nebulaTex, moonTex, planetTex, emilTex, flightUnitTex, podTex };
			glowTex = gradientTex = nebulaTex = moonTex = planetTex = emilTex = flightUnitTex = podTex = null;
			bunkerTexture = null;
			egg = default;
			Array.Clear(farLights, 0, farLights.Length);
			Array.Clear(embers, 0, embers.Length);
			shootLife = 0f;
			flashLife = 0f;
			trailHist.Clear();
			farLightTimer = 1.0f;
			eggSpawnTimer = 3.5f;
			texturesBuilt = false;
			cachedW = cachedH = -1;
			Array.Clear(warFlashes, 0, warFlashes.Length);
			moonAlbedo = null;
			moonBuffer = null;
			mMoonIndex = mMoonRowOff = mMoonBaseCol = null;
			mMoonLightR = mMoonLightG = mMoonLightB = null;
			mMoonAlpha = null;
			moonPixelCount = 0;
			Main.QueueMainThreadAction(() =>
			{
				foreach (var t in all)
					t?.Dispose();
			});
		}

		// ═════════════════════════════════════════════════════════════
		//  Setup
		// ═════════════════════════════════════════════════════════════

		private static float AdvanceClock()
		{
			double now = clock.Elapsed.TotalSeconds;
			float dt = (float)Math.Min(0.1, Math.Max(0.0, now - lastTime));
			lastTime = now;
			totalTime += dt;
			return dt;
		}

		private static void EnsureBasics()
		{
			GraphicsDevice gd = Main.instance.GraphicsDevice;

			if (glowTex == null || glowTex.IsDisposed)
			{
				const int S = 128;
				var data = new Color[S * S];
				for (int y = 0; y < S; y++)
				{
					for (int x = 0; x < S; x++)
					{
						float dx = (x + 0.5f) / S * 2f - 1f;
						float dy = (y + 0.5f) / S * 2f - 1f;
						float d = (float)Math.Sqrt(dx * dx + dy * dy);
						float f = d >= 1f ? 0f : (float)Math.Exp(-d * d * 4.5f) * (1f - d);
						byte b = (byte)(MathHelper.Clamp(f, 0f, 1f) * 255f);
						data[y * S + x] = new Color(b, b, b, b);
					}
				}
				glowTex = new Texture2D(gd, S, S);
				glowTex.SetData(data);
			}

			if (gradientTex == null || gradientTex.IsDisposed)
			{
				const int H = 256;
				var data = new Color[H];
				Vector3 top = new Vector3(1f, 2f, 6f);
				Vector3 mid = new Vector3(5f, 11f, 22f);
				for (int y = 0; y < H; y++)
				{
					float t = y / (float)(H - 1);
					float k = SS(0f, HorizonTop, t);
					Vector3 c = Vector3.Lerp(top, mid, k * k);
					data[y] = new Color((int)c.X, (int)c.Y, (int)c.Z, 255);
				}
				gradientTex = new Texture2D(gd, 1, H);
				gradientTex.SetData(data);
			}

			if (stars == null)
				InitStars();
		}

		private static void InitStars()
		{
			var rand = new Random(11945);
			stars = new Star[360];
			Vector2 a = new Vector2(0f, 0.62f), b = new Vector2(1f, 0.02f);
			Vector2 dir = Vector2.Normalize(b - a);
			Vector2 perp = new Vector2(-dir.Y, dir.X);

			for (int i = 0; i < stars.Length; i++)
			{
				Vector2 pos;
				if (rand.NextDouble() < 0.38)
				{
					// Concentrate some stars along the galactic band
					float t = (float)rand.NextDouble();
					float g = Gaussian(rand) * 0.07f;
					pos = Vector2.Lerp(a, b, t) + perp * g;
				}
				else
				{
					pos = new Vector2((float)rand.NextDouble(), (float)rand.NextDouble() * 0.8f);
				}

				float l = (float)rand.NextDouble();
				int size = l < 0.70 ? 1 : (l < 0.95 ? 2 : 3);
				float baseAlpha = size switch
				{
					1 => 0.20f + (float)rand.NextDouble() * 0.35f,
					2 => 0.45f + (float)rand.NextDouble() * 0.35f,
					_ => 0.75f + (float)rand.NextDouble() * 0.25f
				};

				Color col = rand.Next(5) switch
				{
					0 => new Color(200, 222, 255),
					1 => new Color(255, 250, 240),
					2 => new Color(255, 236, 205),
					3 => new Color(255, 214, 180),
					_ => new Color(225, 232, 250)
				};

				// Ever-so-slight drift: roughly 0.2–0.6 px/sec at 1080p
				float speed = 0.00011f * size;
				stars[i] = new Star
				{
					Pos = pos,
					Drift = new Vector2(speed, -speed * 0.25f),
					BaseAlpha = baseAlpha,
					TwinkleSpeed = 0.25f + (float)rand.NextDouble() * 0.6f,
					Phase = (float)rand.NextDouble() * MathHelper.TwoPi,
					Phase2 = (float)rand.NextDouble() * MathHelper.TwoPi,
					Size = size,
					Spikes = size == 3 && rand.NextDouble() < 0.5,
					Col = col
				};
			}
		}

		private static void BuildDataTextures()
		{
			GraphicsDevice gd = Main.instance.GraphicsDevice;
			nebulaTex = new Texture2D(gd, NebW, NebH);
			nebulaTex.SetData(nebulaData);
			moonTex = new Texture2D(gd, MoonSize, MoonSize);
			BuildMoonGeometry();
			UpdateMoon();
			texturesBuilt = true;
		}

		// ═════════════════════════════════════════════════════════════
		//  Procedural generation (background thread)
		// ═════════════════════════════════════════════════════════════

		private static void GenerateData()
		{
			try
			{
				GenerateSurface();
				GenerateNebula();
				GenerateMoon();
				dataReady = true;
			}
			catch
			{
				// If generation fails for any reason, the scene just stays starfield-only.
			}
		}

		private static void GenerateSurface()
		{
			int n = MapW * MapH;
			var lr = new float[n];
			var lg = new float[n];
			var lb = new float[n];
			var cl = new float[n];

			Vector3 deep = new Vector3(0.015f, 0.055f, 0.11f);
			Vector3 shallow = new Vector3(0.04f, 0.14f, 0.21f);
			Vector3 shelf = new Vector3(0.07f, 0.22f, 0.27f);
			Vector3 dry = new Vector3(0.44f, 0.38f, 0.27f);
			Vector3 wet = new Vector3(0.13f, 0.20f, 0.11f);   // overgrown, reclaimed by nature
			Vector3 high = new Vector3(0.36f, 0.34f, 0.31f);
			Vector3 snow = new Vector3(0.86f, 0.88f, 0.90f);
			Vector3 ice = new Vector3(0.84f, 0.89f, 0.93f);

			for (int y = 0; y < MapH; y++)
			{
				double lat = (0.5 - (y + 0.5) / MapH) * Math.PI;
				float cLat = (float)Math.Cos(lat), sLat = (float)Math.Sin(lat);
				float absLatDeg = (float)(Math.Abs(lat) * 180.0 / Math.PI);

				for (int x = 0; x < MapW; x++)
				{
					double lon = (x + 0.5) / MapW * Math.PI * 2.0;
					float px = cLat * (float)Math.Cos(lon);
					float py = sLat;
					float pz = cLat * (float)Math.Sin(lon);

					// Continents (domain warped for organic coastlines)
					float warp = Fbm(px * 1.2f + 5.2f, py * 1.2f + 1.3f, pz * 1.2f - 2.7f, 3) * 0.7f;
					float hgt = Fbm(px * 1.6f + warp, py * 1.6f - warp, pz * 1.6f + warp, 6);
					float moist = Fbm(px * 2.4f + 11.1f, py * 2.4f, pz * 2.4f - 4.4f, 4);

					Vector3 col;
					if (hgt < 0.03f)
					{
						float t = SS(-0.30f, 0.03f, hgt);
						col = Vector3.Lerp(deep, shallow, t);
						col = Vector3.Lerp(col, shelf, SS(-0.015f, 0.03f, hgt) * 0.8f);
					}
					else
					{
						float e = MathHelper.Clamp((hgt - 0.03f) / 0.33f, 0f, 1f);
						col = Vector3.Lerp(dry, wet, SS(-0.12f, 0.14f, moist + absLatDeg * 0.002f));
						col = Vector3.Lerp(col, high, (float)Math.Pow(e, 1.4));
						col = Vector3.Lerp(col, snow, SS(0.72f, 0.95f, e));
						float detail = Fbm(px * 14f, py * 14f, pz * 14f, 3);
						col *= 0.88f + detail * 0.5f;
					}

					// Polar ice caps
					float iceEdge = 67f + Fbm(px * 4f + 3f, py * 4f, pz * 4f, 3) * 14f;
					col = Vector3.Lerp(col, ice, SS(iceEdge - 2f, iceEdge + 2f, absLatDeg));

					// Clouds: banded along latitude, swirled by a warp field
					float cw = Fbm(px * 2.0f + 31f, py * 2.0f, pz * 2.0f, 3);
					float c = Fbm(px * 2.6f + cw * 1.6f, py * 4.4f + cw, pz * 2.6f - cw * 1.6f, 6);
					float fine = Fbm(px * 11f, py * 13f, pz * 11f, 3);
					float cloud = SS(0.00f, 0.30f, c + fine * 0.25f);
					cloud *= 0.92f;

					int i = y * MapW + x;
					lr[i] = col.X; lg[i] = col.Y; lb[i] = col.Z;
					cl[i] = cloud;
				}
			}

			landR = lr; landG = lg; landB = lb; cloudMap = cl;
		}

		private static void GenerateNebula()
		{
			var data = new Color[NebW * NebH];
			float aspect = 16f / 9f;
			Vector2 a = new Vector2(0f, 0.62f), b = new Vector2(aspect, 0.02f);
			Vector2 dir = Vector2.Normalize(b - a);
			Vector2 perp = new Vector2(-dir.Y, dir.X);

			Vector3 teal = new Vector3(0.14f, 0.28f, 0.34f);
			Vector3 violet = new Vector3(0.28f, 0.19f, 0.34f);
			Vector3 core = new Vector3(0.44f, 0.38f, 0.30f);

			for (int y = 0; y < NebH; y++)
			{
				for (int x = 0; x < NebW; x++)
				{
					Vector2 p = new Vector2((x + 0.5f) / NebW * aspect, (y + 0.5f) / NebH);
					float d = Vector2.Dot(p - a, perp);

					float n1 = Fbm(p.X * 2.4f, p.Y * 2.4f, 3.3f, 5);
					float n2 = Fbm(p.X * 6.5f, p.Y * 6.5f, 9.1f, 4);

					float band = (float)Math.Exp(-Math.Pow((d + n1 * 0.06f) / 0.15f, 2));
					float cloud = MathHelper.Clamp(0.55f + n1 * 1.7f, 0f, 1.3f);
					float lane = (float)Math.Exp(-Math.Pow((d - 0.012f + n1 * 0.05f) / 0.035f, 2)) * SS(-0.12f, 0.18f, n2);
					float total = band * cloud * (1f - 0.8f * lane) + 0.05f * SS(0f, 0.5f, n1 + 0.2f);

					Vector3 col = Vector3.Lerp(teal, violet, SS(-0.25f, 0.30f, n2 * 0.8f + n1));
					col = Vector3.Lerp(col, core, (float)Math.Exp(-Math.Pow(d / 0.06f, 2)) * 0.6f);

					float alpha = MathHelper.Clamp(total * 0.42f, 0f, 0.55f);
					data[y * NebW + x] = new Color(col.X * alpha, col.Y * alpha, col.Z * alpha, alpha);
				}
			}
			nebulaData = data;
		}

		private struct MoonCrater
		{
			public Vector3 Center;
			public float Radius;
		}

		private static void GenerateMoon()
		{
			var rand = new Random(4242);
			const int CraterCount = 52;
			var craters = new MoonCrater[CraterCount];
			for (int i = 0; i < CraterCount; i++)
			{
				float theta = (float)(rand.NextDouble() * MathHelper.TwoPi);
				float phi = (float)(rand.NextDouble() * Math.PI - MathHelper.PiOver2);
				Vector3 center = new Vector3(
					(float)(Math.Cos(phi) * Math.Cos(theta)),
					(float)Math.Sin(phi),
					(float)(Math.Cos(phi) * Math.Sin(theta))
				);
				float r = 0.05f + (float)Math.Pow(rand.NextDouble(), 2.2) * 0.20f;
				craters[i] = new MoonCrater { Center = center, Radius = r };
			}

			var alb = new float[MoonMapW * MoonMapH];

			for (int y = 0; y < MoonMapH; y++)
			{
				double lat = (0.5 - (y + 0.5) / MoonMapH) * Math.PI;
				float cLat = (float)Math.Cos(lat), sLat = (float)Math.Sin(lat);

				for (int x = 0; x < MoonMapW; x++)
				{
					double lon = (x + 0.5) / MoonMapW * Math.PI * 2.0;
					float px = cLat * (float)Math.Cos(lon);
					float py = sLat;
					float pz = cLat * (float)Math.Sin(lon);
					Vector3 nrm = new Vector3(px, py, pz);

					float maria = SS(0.0f, 0.25f, Fbm(nrm.X * 1.8f + 7f, nrm.Y * 1.8f, nrm.Z * 1.8f, 4));
					float albedo = 0.74f - maria * 0.28f + Fbm(nrm.X * 8f, nrm.Y * 8f, nrm.Z * 8f, 3) * 0.10f;

					for (int c = 0; c < CraterCount; c++)
					{
						float d = (nrm - craters[c].Center).Length() / craters[c].Radius;
						if (d < 1.3f)
						{
							if (d < 1f) albedo -= 0.07f * (1f - d * d);
							albedo += 0.06f * (float)Math.Exp(-Math.Pow((d - 1f) / 0.13f, 2));
						}
					}

					alb[y * MoonMapW + x] = MathHelper.Clamp(albedo, 0.10f, 1f);
				}
			}

			moonAlbedo = alb;
		}

		private static void BuildMoonGeometry()
		{
			moonBuffer = new Color[MoonSize * MoonSize];
			int maxPixels = MoonSize * MoonSize;
			mMoonIndex = new int[maxPixels];
			mMoonRowOff = new int[maxPixels];
			mMoonBaseCol = new int[maxPixels];
			mMoonLightR = new float[maxPixels];
			mMoonLightG = new float[maxPixels];
			mMoonLightB = new float[maxPixels];
			mMoonAlpha = new byte[maxPixels];
			moonPixelCount = 0;

			float half = MoonSize / 2f;
			// Light direction from Sun on the horizon (bottom-left) towards the Moon (upper-right)
			Vector3 moonSunDir = Vector3.Normalize(new Vector3(-0.85f, 0.45f, 0.30f));
			Vector3 sunCol = new Vector3(0.92f, 0.90f, 0.86f);
			Vector3 earthShine = new Vector3(0.045f, 0.065f, 0.095f);

			for (int y = 0; y < MoonSize; y++)
			{
				for (int x = 0; x < MoonSize; x++)
				{
					int bi = y * MoonSize + x;
					float dx = (x + 0.5f - half) / (half - 1f);
					float dy = (y + 0.5f - half) / (half - 1f);
					float r2 = dx * dx + dy * dy;
					if (r2 >= 1f)
					{
						moonBuffer[bi] = Color.Transparent;
						continue;
					}

					float r = (float)Math.Sqrt(r2);
					float cover = MathHelper.Clamp((1f - r) * (half - 1f), 0f, 1f);
					float z = (float)Math.Sqrt(1f - r2);
					Vector3 nrm = new Vector3(dx, dy, z);

					float ndl = Vector3.Dot(nrm, moonSunDir);
					float sunLight = (float)Math.Pow(Math.Max(0f, ndl), 0.85);
					Vector3 light = sunCol * sunLight + earthShine;

					// Spherical projection (top of Moon is North, so lat is -dy)
					double lat = Math.Asin(MathHelper.Clamp(-dy, -1f, 1f));
					double lon = Math.Atan2(dx, z); // Center of visible disk has lon = 0
					int row = Math.Clamp((int)((0.5 - lat / Math.PI) * MoonMapH), 0, MoonMapH - 1);
					int baseCol = (int)((lon / (Math.PI * 2.0) + 0.5) * MoonMapW);

					int p = moonPixelCount++;
					mMoonIndex[p] = bi;
					mMoonRowOff[p] = row * MoonMapW;
					mMoonBaseCol[p] = baseCol;
					mMoonLightR[p] = light.X * cover;
					mMoonLightG[p] = light.Y * cover;
					mMoonLightB[p] = light.Z * cover;
					mMoonAlpha[p] = (byte)(cover * 255f);
				}
			}
		}

		private static void UpdateMoon()
		{
			if (moonAlbedo == null || moonTex == null || moonPixelCount == 0)
				return;

			// Smooth, slow axial rotation of the lunar surface features underneath the fixed sunlight
			float rotProgress = totalTime * 1.6f;
			int r0 = (int)Math.Floor(rotProgress);
			float rFrac = rotProgress - r0;
			int r1 = (r0 + 1) & (MoonMapW - 1);
			const int mask = MoonMapW - 1;

			for (int p = 0; p < moonPixelCount; p++)
			{
				int row = mMoonRowOff[p];
				int baseCol = mMoonBaseCol[p];
				int i0 = row + ((baseCol + r0) & mask);
				int i1 = row + ((baseCol + r1) & mask);
				float albedo = moonAlbedo[i0] + (moonAlbedo[i1] - moonAlbedo[i0]) * rFrac;

				int r = (int)(albedo * mMoonLightR[p] * 255f);
				int g = (int)(albedo * mMoonLightG[p] * 255f);
				int b = (int)(albedo * mMoonLightB[p] * 255f);

				moonBuffer[mMoonIndex[p]] = new Color(
					(byte)(r > 255 ? 255 : (r < 0 ? 0 : r)),
					(byte)(g > 255 ? 255 : (g < 0 ? 0 : g)),
					(byte)(b > 255 ? 255 : (b < 0 ? 0 : b)),
					mMoonAlpha[p]
				);
			}

			moonTex.SetData(moonBuffer);
		}

		// ═════════════════════════════════════════════════════════════
		//  Planet
		// ═════════════════════════════════════════════════════════════

		private static float HorizonY(float x)
		{
			float dx = x - gCx;
			return gCy - (float)Math.Sqrt(Math.Max(0f, gR * gR - dx * dx));
		}

		private static void BuildGeometry(int w, int h)
		{
			cachedW = w;
			cachedH = h;
			gR = RadiusFactor * w;
			gCx = w * 0.5f;
			gCy = HorizonTop * h + gR;

			float atmPx = AtmThickness * gR;
			bufDiv = Math.Max(2, (int)Math.Ceiling(w / 960f));
			bufTop = Math.Max(0, (int)(HorizonTop * h - atmPx * 3.6f));
			bufW = (w + bufDiv - 1) / bufDiv;
			bufH = Math.Max(1, (h - bufTop + bufDiv - 1) / bufDiv);

			int n = bufW * bufH;
			buffer = new Color[n];
			pIndex = new int[n]; pRowOff = new int[n]; pLon = new float[n];
			pKr = new float[n]; pKg = new float[n]; pKb = new float[n];
			pAr = new float[n]; pAg = new float[n]; pAb = new float[n];
			planetCount = 0;
			nightSpots.Clear();

			Vector3 dayAtm = new Vector3(0.28f, 0.52f, 0.92f);
			Vector3 duskAtm = new Vector3(1.0f, 0.50f, 0.22f);
			Vector3 rimCol = new Vector3(0.62f, 0.84f, 1.0f);
			Vector3 moonlight = new Vector3(0.045f, 0.06f, 0.085f);
			Vector3 duskTint = new Vector3(1.0f, 0.55f, 0.32f);

			for (int j = 0; j < bufH; j++)
			{
				float sy = bufTop + (j + 0.5f) * bufDiv;
				for (int i = 0; i < bufW; i++)
				{
					float sx = (i + 0.5f) * bufDiv;
					float dx = (sx - gCx) / gR;
					float dy = (sy - gCy) / gR;
					float r2 = dx * dx + dy * dy;
					float r = (float)Math.Sqrt(r2);
					int bi = j * bufW + i;

					if (r < 1f)
					{
						float z = (float)Math.Sqrt(Math.Max(0f, 1f - r2));
						Vector3 nrm = new Vector3(dx, dy, z);
						float ndl = Vector3.Dot(nrm, SunDir);

						float ly = Vector3.Dot(nrm, Axis);
						float lx = Vector3.Dot(nrm, AxisX);
						float lz = Vector3.Dot(nrm, AxisZ);
						double lat = Math.Asin(MathHelper.Clamp(ly, -1f, 1f));
						double lon = Math.Atan2(lz, lx);
						int row = Math.Clamp((int)((0.5 - lat / Math.PI) * MapH), 0, MapH - 1);
						float lonCol = (float)((lon / (Math.PI * 2.0) + 0.5) * MapW);

						float day = SS(-0.10f, 0.30f, ndl);
						float light = day * (0.30f + 1.05f * (float)Math.Pow(Math.Max(0f, ndl), 0.75));
						float term = (float)Math.Exp(-Math.Pow((ndl - 0.03f) / 0.10f, 2));
						Vector3 k = Vector3.Lerp(Vector3.One, duskTint, term * 0.85f) * light + moonlight * (1f - day);

						float haze = (float)Math.Pow(1f - z, 2.4);
						float scatter = SS(-0.30f, 0.45f, ndl);
						Vector3 atm = Vector3.Lerp(dayAtm, duskAtm, term * 0.9f) * scatter;
						k *= 1f - haze * 0.8f;
						Vector3 add = atm * haze * 0.85f + new Vector3(0.004f, 0.009f, 0.02f);

						float edgePx = (1f - r) * gR;
						float rim = (float)Math.Exp(-edgePx / 3.5f);
						add += rimCol * rim * (0.12f + 0.88f * scatter);

						int p = planetCount++;
						pIndex[p] = bi;
						pRowOff[p] = row * MapW;
						pLon[p] = lonCol;
						pKr[p] = k.X; pKg[p] = k.Y; pKb[p] = k.Z;
						pAr[p] = add.X; pAg[p] = add.Y; pAb[p] = add.Z;

						if (ndl < -0.12f && (i % 6 == 0) && (j % 6 == 0) && sy < h - 40)
							nightSpots.Add(new Vector2(sx, sy));
					}
					else
					{
						// Atmosphere glow outside the limb (static, premultiplied)
						float t = (r - 1f) / AtmThickness;
						Vector3 limbN = new Vector3(dx / r, dy / r, 0f);
						float ndl = Vector3.Dot(limbN, SunDir);
						float scatter = SS(-0.40f, 0.55f, ndl);
						float term = (float)Math.Exp(-Math.Pow((ndl - 0.05f) / 0.18f, 2));

						float alpha = (float)Math.Exp(-t * 1.7f) * (0.18f + 0.82f * scatter);
						Vector3 col = Vector3.Lerp(new Vector3(0.35f, 0.62f, 1.0f), new Vector3(0.75f, 0.90f, 1.0f), (float)Math.Exp(-t * 3f));
						col = Vector3.Lerp(col, new Vector3(1.0f, 0.62f, 0.35f), term * 0.55f);

						if (alpha < 0.004f)
						{
							buffer[bi] = Color.Transparent;
						}
						else
						{
							float a = MathHelper.Clamp(alpha, 0f, 1f);
							buffer[bi] = new Color(col.X * a, col.Y * a, col.Z * a, a * 0.55f);
						}
					}
				}
			}

			planetTex?.Dispose();
			planetTex = new Texture2D(Main.instance.GraphicsDevice, bufW, bufH, false, SurfaceFormat.Color);
			planetTex.SetData(buffer);
		}

		private static void UpdatePlanet()
		{
			int l0 = (int)Math.Floor(landRot);
			float lFrac = landRot - l0;
			int l1 = (l0 + 1) & (MapW - 1);

			int c0 = (int)Math.Floor(cloudRot);
			float cFrac = cloudRot - c0;
			int c1 = (c0 + 1) & (MapW - 1);

			const int mask = MapW - 1;
			float[] lr = landR, lg = landG, lb = landB, cm = cloudMap;
			const float cR = 0.93f, cG = 0.94f, cB = 0.96f;

			Parallel.For(0, planetCount, p =>
			{
				int row = pRowOff[p];
				int baseCol = (int)pLon[p];

				int li0 = row + ((baseCol + l0) & mask);
				int li1 = row + ((baseCol + l1) & mask);
				float landR_val = lr[li0] + (lr[li1] - lr[li0]) * lFrac;
				float landG_val = lg[li0] + (lg[li1] - lg[li0]) * lFrac;
				float landB_val = lb[li0] + (lb[li1] - lb[li0]) * lFrac;

				int ci0 = row + ((baseCol + c0) & mask);
				int ci1 = row + ((baseCol + c1) & mask);
				float cloud = cm[ci0] + (cm[ci1] - cm[ci0]) * cFrac;
				float inv = 1f - cloud;

				float sr = landR_val * inv + cR * cloud;
				float sg = landG_val * inv + cG * cloud;
				float sb = landB_val * inv + cB * cloud;

				int r = (int)((sr * pKr[p] + pAr[p]) * 255f);
				int g = (int)((sg * pKg[p] + pAg[p]) * 255f);
				int b = (int)((sb * pKb[p] + pAb[p]) * 255f);
				buffer[pIndex[p]] = new Color(r > 255 ? 255 : (r < 0 ? 0 : r),
				                              g > 255 ? 255 : (g < 0 ? 0 : g),
				                              b > 255 ? 255 : (b < 0 ? 0 : b), 255);
			});

			planetTex.SetData(buffer);
		}

		private static void SpawnWarFlash(int h, bool cluster = false)
		{
			if (nightSpots.Count == 0)
				return;

			int slot = -1;
			for (int i = 0; i < MaxWarFlashes; i++)
			{
				if (!warFlashes[i].Active)
				{
					slot = i;
					break;
				}
			}
			if (slot == -1)
				slot = fxRand.Next(MaxWarFlashes);

			Vector2 pos;
			if (cluster && lastWarPos != Vector2.Zero)
			{
				float offsetDist = (float)(fxRand.NextDouble() * h * 0.035f + h * 0.005f);
				float offsetAngle = (float)(fxRand.NextDouble() * MathHelper.TwoPi);
				pos = lastWarPos + new Vector2((float)Math.Cos(offsetAngle), (float)Math.Sin(offsetAngle)) * offsetDist;
			}
			else
			{
				pos = nightSpots[fxRand.Next(nightSpots.Count)];
			}
			lastWarPos = pos;

			double roll = fxRand.NextDouble();
			int type;
			float maxLife;
			float baseSize;
			Color primCol;
			Color coreCol;

			if (roll < 0.40)
			{
				// Type 0: Plasma Detonation / Heavy Bombardment
				type = 0;
				maxLife = 0.35f + (float)fxRand.NextDouble() * 0.25f;
				baseSize = h * (0.026f + (float)fxRand.NextDouble() * 0.025f);
				primCol = new Color(255, 135, 35, 0);
				coreCol = new Color(255, 235, 180, 0);
			}
			else if (roll < 0.70)
			{
				// Type 1: Crimson Machine Laser Strike
				type = 1;
				maxLife = 0.20f + (float)fxRand.NextDouble() * 0.20f;
				baseSize = h * (0.028f + (float)fxRand.NextDouble() * 0.025f);
				primCol = new Color(255, 30, 45, 0);
				coreCol = new Color(255, 190, 200, 0);
			}
			else if (roll < 0.90)
			{
				// Type 2: Burning Ruined Warzone Fires / Embers
				type = 2;
				maxLife = 0.80f + (float)fxRand.NextDouble() * 0.70f;
				baseSize = h * (0.016f + (float)fxRand.NextDouble() * 0.018f);
				primCol = new Color(255, 95, 20, 0);
				coreCol = new Color(255, 205, 80, 0);
			}
			else
			{
				// Type 3: High-Altitude EMP / Lightning Arc
				type = 3;
				maxLife = 0.22f + (float)fxRand.NextDouble() * 0.22f;
				baseSize = h * (0.028f + (float)fxRand.NextDouble() * 0.028f);
				primCol = new Color(110, 190, 255, 0);
				coreCol = new Color(235, 245, 255, 0);
			}

			warFlashes[slot] = new WarFlash
			{
				Active = true,
				Pos = pos,
				Life = maxLife,
				MaxLife = maxLife,
				BaseSize = baseSize,
				Type = type,
				PrimaryCol = primCol,
				CoreCol = coreCol,
				Angle = (float)(fxRand.NextDouble() * 0.5 - 0.25)
			};
		}

		private static void DrawWarDestruction(SpriteBatch sb, int h, float dt)
		{
			if (nightSpots.Count == 0)
				return;

			// Spawn war events across Earth's dark side and frontline (subtle, spaced-out cadence)
			warSpawnTimer -= dt;
			if (warSpawnTimer <= 0f)
			{
				warSpawnTimer = 1.4f + (float)fxRand.NextDouble() * 2.2f;
				SpawnWarFlash(h);

				// Rare chance for a rapid consecutive cluster strike nearby
				if (fxRand.NextDouble() < 0.20)
				{
					SpawnWarFlash(h, cluster: true);
				}
			}

			for (int i = 0; i < MaxWarFlashes; i++)
			{
				if (!warFlashes[i].Active)
					continue;

				ref WarFlash flash = ref warFlashes[i];
				flash.Life -= dt;
				if (flash.Life <= 0f)
				{
					flash.Active = false;
					continue;
				}

				float norm = 1f - (flash.Life / flash.MaxLife); // [0..1] progress

				switch (flash.Type)
				{
					case 0: // Expanding plasma detonation fireball
					{
						float expand = flash.BaseSize * (0.5f + 1.25f * norm);
						float intensity = (norm < 0.15f ? (norm / 0.15f) : (float)Math.Pow(1f - norm, 1.8)) * planetFade;
						DrawGlow(sb, flash.Pos, expand * 1.5f, expand * 1.2f, flash.PrimaryCol * (0.65f * intensity));
						DrawGlow(sb, flash.Pos, expand * 0.45f, expand * 0.35f, flash.CoreCol * (0.95f * intensity));
						break;
					}
					case 1: // Crimson Machine Laser Strike
					{
						float intensity = (float)Math.Sin(norm * Math.PI) * planetFade;

						// Downward laser strike trace from atmosphere
						if (norm < 0.65f)
						{
							float beamFade = (1f - norm / 0.65f) * planetFade;
							float beamLen = h * 0.055f;
							Vector2 beamCenter = flash.Pos - new Vector2((float)Math.Sin(flash.Angle), (float)Math.Cos(flash.Angle)) * (beamLen * 0.45f);
							DrawGlow(sb, beamCenter, 3.5f, beamLen, flash.PrimaryCol * (0.80f * beamFade), flash.Angle);
						}

						// Surface impact detonation
						DrawGlow(sb, flash.Pos, flash.BaseSize * 1.35f, flash.BaseSize * 1.35f, flash.PrimaryCol * (0.85f * intensity));
						DrawGlow(sb, flash.Pos, flash.BaseSize * 0.35f, flash.BaseSize * 0.35f, flash.CoreCol * (1.0f * intensity));
						break;
					}
					case 2: // Burning warzone fires / ruined city embers
					{
						float flicker = (float)(0.65 + 0.35 * Math.Sin(totalTime * 24.0 + flash.Pos.X)) * (1f - norm) * planetFade;
						DrawGlow(sb, flash.Pos, flash.BaseSize * 1.2f, flash.BaseSize * 0.95f, flash.PrimaryCol * (0.55f * flicker));
						DrawGlow(sb, flash.Pos, flash.BaseSize * 0.32f, flash.BaseSize * 0.25f, flash.CoreCol * (0.85f * flicker));
						break;
					}
					case 3: // EMP / Shock discharge
					{
						float jitter = (float)(0.50 + 0.50 * Math.Sin(norm * 44.0)) * (1f - norm) * planetFade;
						DrawGlow(sb, flash.Pos, flash.BaseSize * 1.6f, flash.BaseSize * 1.2f, flash.PrimaryCol * (0.75f * jitter));
						DrawGlow(sb, flash.Pos, flash.BaseSize * 0.38f, flash.BaseSize * 0.28f, flash.CoreCol * (0.95f * jitter));
						break;
					}
				}
			}
		}

		// ═════════════════════════════════════════════════════════════
		//  Sky elements
		// ═════════════════════════════════════════════════════════════

		// ───────────────────────── Distant drifting lights ─────────────────────────
		private struct FarLight
		{
			public bool Active;
			public Vector2 Pos, Vel;   // pixels, pixels/second
			public float Life, MaxLife, Size, Phase;
			public Color Col;
		}

		private const int MaxFarLights = 5;
		private static readonly FarLight[] farLights = new FarLight[MaxFarLights];
		private static float farLightTimer = 1.0f;

		private static void DrawDistantLights(SpriteBatch sb, Texture2D pixel, int w, int h, float dt)
		{
			if (cachedW <= 0)
				return;

			farLightTimer -= dt;
			if (farLightTimer <= 0f)
			{
				farLightTimer = 2.5f + (float)fxRand.NextDouble() * 4f;
				for (int i = 0; i < MaxFarLights; i++)
				{
					if (farLights[i].Active)
						continue;

					bool ltr = fxRand.NextDouble() < 0.5;
					float speed = w * (0.010f + (float)fxRand.NextDouble() * 0.014f); // very slow: crosses in ~40-100s
					float y = h * (0.05f + (float)fxRand.NextDouble() * 0.40f);
					float vy = (float)(fxRand.NextDouble() - 0.5) * speed * 0.15f;
					float startX = ltr ? -10f : w + 10f;
					float life = (w + 20f) / speed;
					Color col = fxRand.Next(3) switch
					{
						0 => new Color(255, 245, 220),
						1 => new Color(190, 225, 255),
						_ => new Color(255, 215, 170)
					};
					farLights[i] = new FarLight
					{
						Active = true,
						Pos = new Vector2(startX, y),
						Vel = new Vector2(ltr ? speed : -speed, vy),
						Life = life,
						MaxLife = life,
						Size = 1f + (float)fxRand.NextDouble() * 0.8f,
						Phase = (float)(fxRand.NextDouble() * MathHelper.TwoPi),
						Col = col
					};
					break;
				}
			}

			Rectangle src = new Rectangle(0, 0, 1, 1);
			for (int i = 0; i < MaxFarLights; i++)
			{
				if (!farLights[i].Active)
					continue;

				ref FarLight l = ref farLights[i];
				l.Life -= dt;
				l.Pos += l.Vel * dt;
				if (l.Life <= 0f || l.Pos.Y > HorizonY(l.Pos.X) - 6f)
				{
					l.Active = false;
					continue;
				}

				float age = 1f - l.Life / l.MaxLife;
				float edge = Math.Min(SS(0f, 0.06f, age), SS(0f, 0.06f, 1f - age));
				float blink = 0.75f + 0.25f * (float)Math.Sin(totalTime * 2.2f + l.Phase);
				float a = edge * blink;

				// Soft glow + tiny bright core + faint fading trail behind
				DrawGlow(sb, l.Pos, 7f * l.Size, 7f * l.Size, new Color(l.Col.R, l.Col.G, l.Col.B, 0) * (0.35f * a));
				Vector2 dir = Vector2.Normalize(l.Vel);
				for (int t = 1; t <= 6; t++)
				{
					Vector2 tp = l.Pos - dir * (t * 3.5f);
					sb.Draw(pixel, new Rectangle((int)tp.X, (int)tp.Y, 1, 1), src, l.Col * (a * 0.30f * (1f - t / 7f)));
				}
				int core = l.Size > 1.5f ? 2 : 1;
				sb.Draw(pixel, new Rectangle((int)l.Pos.X, (int)l.Pos.Y, core, core), src, Color.White * a);
			}
		}

		private static void DrawStars(SpriteBatch sb, Texture2D pixel, int w, int h, float dt)
		{
			Rectangle src = new Rectangle(0, 0, 1, 1);
			for (int i = 0; i < stars.Length; i++)
			{
				ref Star s = ref stars[i];
				s.Pos += s.Drift * dt;
				if (s.Pos.X > 1f) s.Pos.X -= 1f;
				if (s.Pos.Y < 0f) s.Pos.Y += 0.8f;

				float sx = s.Pos.X * w;
				float sy = s.Pos.Y * h;
				if (cachedW > 0 && sy > HorizonY(sx) - AtmThickness * gR * 0.5f)
					continue;

				float w1 = (float)Math.Sin(totalTime * s.TwinkleSpeed + s.Phase);
				float w2 = (float)Math.Sin(totalTime * s.TwinkleSpeed * 1.618f + s.Phase2);
				float shimmer = (w1 * 0.65f + w2 * 0.35f) * 0.5f + 0.5f; // [0,1], smooth
				// Super subtle: brightness only breathes by about ±8%
				float alpha = MathHelper.Clamp(s.BaseAlpha * (0.92f + 0.16f * (shimmer - 0.5f)), 0f, 1f);

				int sz = s.Size;

				// Sub-pixel rendering: split brightness across the 4 neighbouring pixels so the
				// ultra-slow drift glides smoothly instead of snapping 1px at a time.
				int ix = (int)Math.Floor(sx), iy = (int)Math.Floor(sy);
				float fx = sx - ix, fy = sy - iy;
				Color c = s.Col * alpha;
				sb.Draw(pixel, new Rectangle(ix, iy, sz, sz), src, c * ((1f - fx) * (1f - fy)));
				sb.Draw(pixel, new Rectangle(ix + 1, iy, sz, sz), src, c * (fx * (1f - fy)));
				sb.Draw(pixel, new Rectangle(ix, iy + 1, sz, sz), src, c * ((1f - fx) * fy));
				sb.Draw(pixel, new Rectangle(ix + 1, iy + 1, sz, sz), src, c * (fx * fy));

				if (s.Size >= 2)
					sb.Draw(pixel, new Rectangle(ix - 1, iy - 1, sz + 2, sz + 2), src, s.Col * (alpha * 0.10f));

				if (s.Spikes)
				{
					float spike = alpha * 0.18f;
					const int len = 6;
					sb.Draw(pixel, new Rectangle(ix - len, iy + sz / 2, len * 2 + sz, 1), src, s.Col * spike);
					sb.Draw(pixel, new Rectangle(ix + sz / 2, iy - len, 1, len * 2 + sz), src, s.Col * spike);
				}
			}
		}



		private static void DrawAsteroidRock(SpriteBatch sb, Texture2D pixel, Vector2 pos, float rot, float alpha, float heat = 0f, float scale = 1f)
		{
			if (alpha <= 0f || scale <= 0.04f) return;

			Color darkBasalt = new Color(38, 42, 52) * alpha;
			Color midSlate = new Color(85, 92, 108) * alpha;
			Color litStone = new Color(175, 185, 205) * alpha;
			Color highlight = new Color(230, 238, 255) * alpha;
			Color heatGlow = new Color(255, 155, 50) * (alpha * heat);

			float cos = (float)Math.Cos(rot);
			float sin = (float)Math.Sin(rot);

			// 24 faceted pixels forming an irregular 3D stony space boulder
			(int ox, int oy, int colType)[] rockPixels =
			{
				(-2, -3, 2), (-1, -3, 3), (0, -3, 2), (1, -3, 1),
				(-3, -2, 2), (-2, -2, 3), (-1, -2, 2), (0, -2, 1), (1, -2, 0), (2, -2, 0),
				(-4, -1, 3), (-3, -1, 2), (-2, -1, 2), (-1, -1, 1), (0, -1, 1), (1, -1, 0), (2, -1, 0),
				(-4,  0, 2), (-3,  0, 2), (-2,  0, 1), (-1,  0, 1), (0,  0, 1), (1,  0, 0), (2,  0, 0),
				(-3,  1, 2), (-2,  1, 1), (-1,  1, 1), (0,  1, 0), (1,  1, 0),
				(-2,  2, 1), (-1,  2, 1), (0,  2, 0), (1,  2, 0),
				(-1,  3, 0), (0,  3, 0)
			};

			Rectangle src = new Rectangle(0, 0, 1, 1);
			float pixelScale = 1.5f * scale;
			int dotSize = scale < 0.65f ? 1 : 2;

			for (int i = 0; i < rockPixels.Length; i++)
			{
				var p = rockPixels[i];
				float rx = pos.X + (p.ox * cos - p.oy * sin) * pixelScale;
				float ry = pos.Y + (p.ox * sin + p.oy * cos) * pixelScale;

				Color c = p.colType switch
				{
					3 => highlight,
					2 => litStone,
					1 => (heat > 0.35f && (i % 3 == 0)) ? heatGlow : midSlate,
					_ => darkBasalt
				};

				sb.Draw(pixel, new Rectangle((int)rx, (int)ry, dotSize, dotSize), src, c);
			}

			if (heat > 0.15f)
			{
				DrawGlow(sb, pos, 16f * heat * scale, 16f * heat * scale, new Color(255, 175, 70, 0) * (alpha * heat * 0.65f));
			}
		}

		// Blackbody-ish colour ramp for re-entry plasma: 0 = white-hot, 1 = deep red
		private static Color HeatColor(float k)
		{
			k = MathHelper.Clamp(k, 0f, 1f);
			if (k < 0.33f) return Color.Lerp(new Color(255, 250, 235, 0), new Color(255, 214, 140, 0), k / 0.33f);
			if (k < 0.66f) return Color.Lerp(new Color(255, 214, 140, 0), new Color(255, 120, 45, 0), (k - 0.33f) / 0.33f);
			return Color.Lerp(new Color(255, 120, 45, 0), new Color(170, 38, 18, 0), (k - 0.66f) / 0.34f);
		}

		private static readonly List<Vector2> trailHist = new List<Vector2>();

		// Walks back along the recorded flight path, returning points spaced `step` px apart (arc length)
		private static int SampleTrail(Vector2 head, float step, Vector2[] outPts)
		{
			outPts[0] = head;
			int n = 1;
			Vector2 cur = head;
			float need = step;
			for (int i = 0; i < trailHist.Count && n < outPts.Length; i++)
			{
				Vector2 nxt = trailHist[i];
				float seg = Vector2.Distance(cur, nxt);
				while (seg >= need && n < outPts.Length)
				{
					cur = Vector2.Lerp(cur, nxt, need / seg);
					outPts[n++] = cur;
					seg = Vector2.Distance(cur, nxt);
					need = step;
				}
				need -= seg;
				cur = nxt;
			}
			return n;
		}

		private static readonly Vector2[] trailPts = new Vector2[200];

		private static void DrawAtmosphericTrail(SpriteBatch sb, Texture2D pixel, Vector2 headPos, Vector2 dirN, float fade, float lengthPx, float s)
		{
			if (fade <= 0f || lengthPx <= 2f) return;

			// Dense stamping of soft round glows along the flown path: no segments, no seams
			const int steps = 150;
			int n = SampleTrail(headPos, lengthPx / steps, trailPts);
			if (n < 3) return;

			for (int k = 0; k < n; k++)
			{
				float u = k / (float)steps;
				float frac = 1f - u;
				Vector2 a = trailPts[Math.Max(0, k - 1)];
				Vector2 b = trailPts[Math.Min(n - 1, k + 1)];
				Vector2 d = a - b;
				float dl = d.Length();
				Vector2 dir = dl > 0.001f ? d / dl : dirN;
				Vector2 perp = new Vector2(-dir.Y, dir.X);

				// Turbulence grows towards the tail
				float wob = ((float)Math.Sin(k * 0.12f - totalTime * 11f) * 2.2f + (float)Math.Sin(k * 0.31f - totalTime * 19f) * 1.0f) * s * u;
				Vector2 p = trailPts[k] + perp * wob;

				float fall = (float)Math.Pow(frac, 1.35f);
				// Wide dim sheath, mid coloured body, thin white-hot core
				DrawGlow(sb, p, 30f * s * (0.25f + 0.75f * frac), 30f * s * (0.25f + 0.75f * frac), HeatColor(u * 0.95f) * (fade * fall * 0.050f));
				DrawGlow(sb, p, 13f * s * (0.30f + 0.70f * frac), 13f * s * (0.30f + 0.70f * frac), HeatColor(u * 0.8f) * (fade * fall * 0.120f));
				DrawGlow(sb, p, 5.5f * s * (0.35f + 0.65f * frac), 5.5f * s * (0.35f + 0.65f * frac), HeatColor(u * 0.5f) * (fade * fall * 0.260f));
			}
		}

		private struct Ember
		{
			public Vector2 Pos, Vel;
			public float Life, Max, Size;
		}

		private const int MaxEmbers = 220;
		private static readonly Ember[] embers = new Ember[MaxEmbers];
		private static int emberNext;
		private static float emberAcc;
		private static float fragTimer = 1.2f, flashLife;
		private static Vector2 flashPos;
		private static float shootVx, shootAltStart, shootAltEnd, shootStartX, shootY0, shootSlope, shootFall;

		private static void SpawnEmber(Vector2 origin, Vector2 dirN, float s, float speedMul)
		{
			Vector2 perp = new Vector2(-dirN.Y, dirN.X);
			float life = 0.5f + (float)fxRand.NextDouble() * 1.0f;
			float back = (25f + (float)fxRand.NextDouble() * 95f) * s * speedMul;
			float side = ((float)fxRand.NextDouble() - 0.5f) * 70f * s * speedMul;
			embers[emberNext] = new Ember
			{
				Pos = origin + perp * (((float)fxRand.NextDouble() - 0.5f) * 8f * s),
				Vel = -dirN * back + perp * side,
				Life = life,
				Max = life,
				Size = fxRand.NextDouble() < 0.2 ? 2f : 1f
			};
			emberNext = (emberNext + 1) % MaxEmbers;
		}

		private static void DrawEmbers(SpriteBatch sb, Texture2D pixel, float dt, float s)
		{
			Rectangle src = new Rectangle(0, 0, 1, 1);
			for (int i = 0; i < MaxEmbers; i++)
			{
				ref Ember e = ref embers[i];
				if (e.Life <= 0f)
					continue;

				e.Life -= dt;
				if (e.Life <= 0f)
					continue;

				Vector2 toEarth = new Vector2(gCx, gCy) - e.Pos;
				if (toEarth.LengthSquared() > 1f)
					e.Vel += Vector2.Normalize(toEarth) * (14f * s * dt);
				e.Vel *= 1f - 0.8f * dt;
				e.Pos += e.Vel * dt;

				float age = 1f - e.Life / e.Max;
				float a = (float)Math.Pow(1f - age, 1.3) * planetFade;
				int sz = (int)e.Size;
				sb.Draw(pixel, new Rectangle((int)e.Pos.X, (int)e.Pos.Y, sz, sz), src, HeatColor(age * 1.05f) * a);
				if (sz > 1)
					DrawGlow(sb, e.Pos, 5f * s, 5f * s, HeatColor(age) * (a * 0.35f));
			}
		}

		private static void DrawShootingStar(SpriteBatch sb, Texture2D pixel, int w, int h, float dt)
		{
			float s = h / 1080f;
			DrawEmbers(sb, pixel, dt, s);

			if (flashLife > 0f)
			{
				flashLife -= dt;
				float f = MathHelper.Clamp(flashLife / 0.35f, 0f, 1f);
				DrawGlow(sb, flashPos, 55f * s * (1.3f - 0.3f * f), 55f * s * (1.3f - 0.3f * f), new Color(255, 190, 110, 0) * (0.55f * f * f));
				DrawGlow(sb, flashPos, 20f * s, 20f * s, new Color(255, 245, 225, 0) * (0.85f * f * f));
			}

			if (shootLife > 0f)
			{
				shootLife -= dt;
				float t = MathHelper.Clamp(1f - shootLife / shootMax, 0f, 1f);

				// Straight approach, then gravity pulls it into a steeper and steeper curve towards Earth
				Vector2 prev = shootPos;
				float xNow = shootPos.X + shootVx * dt;
				float dxAbs = Math.Abs(xNow - shootStartX);
				float yNow = shootY0 + shootSlope * dxAbs + shootFall * (float)Math.Pow(t, 2.6);
				shootPos = new Vector2(xNow, yNow);
				if (trailHist.Count == 0 || Vector2.Distance(trailHist[0], shootPos) >= 3f * s)
				{
					trailHist.Insert(0, shootPos);
					if (trailHist.Count > 400)
						trailHist.RemoveAt(trailHist.Count - 1);
				}
				Vector2 delta = shootPos - prev;
				Vector2 dirN = delta.LengthSquared() > 0.0001f ? Vector2.Normalize(delta) : new Vector2(Math.Sign(shootVx), 0f);
				float angle = (float)Math.Atan2(dirN.Y, dirN.X);

				const float igniteThreshold = 0.18f;
				if (t < igniteThreshold)
				{
					// Cold tumbling rock approaching the atmosphere
					float entryFade = MathHelper.Clamp(t / 0.05f, 0f, 1f);
					DrawAsteroidRock(sb, pixel, shootPos, totalTime * 2.8f, entryFade, 0f, 1.2f);
					return;
				}

				float p = (t - igniteThreshold) / (1f - igniteThreshold);

				// Ignition ramp, long sustained burn with slow swells, then burn-out
				float ramp = SS(0f, 0.14f, p);
				float swell = 0.88f + 0.12f * (float)Math.Sin(p * Math.PI * 4.0);
				float burnOut = 1f - SS(0.72f, 1f, p);
				float flicker = 0.80f + 0.40f * SunFlicker(totalTime * 5f, 9f) + 0.08f * (float)Math.Sin(totalTime * 41f);
				float intensity = ramp * swell * burnOut;

				float surfaceBuffer = 1f;
				float altNow = HorizonY(shootPos.X) - shootPos.Y;

				float rockScale = 1.35f * (1f - SS(0.40f, 1f, p));
				float rockAlpha = (1f - SS(0.80f, 1f, p)) * surfaceBuffer;

				if (intensity > 0.005f)
				{
					// Shock-lit atmosphere on the limb beneath the meteor
					Vector2 hp = new Vector2(shootPos.X, HorizonY(shootPos.X));
					Vector2 rad = hp - new Vector2(gCx, gCy);
					float tan = (float)Math.Atan2(rad.X, -rad.Y);
					DrawGlow(sb, hp, 190f * s, 16f * s, new Color(255, 135, 65, 0) * (0.20f * intensity * flicker * SS(-0.04f * h, 0.03f * h, altNow)), tan);

					// Heat-shedding embers
					emberAcc += dt * 70f * intensity;
					while (emberAcc >= 1f)
					{
						emberAcc -= 1f;
						SpawnEmber(shootPos - dirN * (4f * s), dirN, s, 1f);
					}

					// Fragmentation: bright flash plus a burst of sparks
					fragTimer -= dt;
					if (fragTimer <= 0f && p > 0.1f && p < 0.85f)
					{
						fragTimer = 0.7f + (float)fxRand.NextDouble() * 1.3f;
						flashLife = 0.35f;
						flashPos = shootPos;
						for (int i = 0; i < 14; i++)
							SpawnEmber(shootPos, dirN, s, 1.8f);
					}

					DrawAtmosphericTrail(sb, pixel, shootPos, dirN, intensity * flicker, 520f * s * intensity, s);

					// Fireball: orange bloom, yellow body, white-hot core, blue-white bow shock ahead
					float g = intensity * flicker;
					DrawGlow(sb, shootPos, 85f * s * g, 85f * s * g, new Color(255, 110, 40, 0) * (0.30f * intensity));
					DrawGlow(sb, shootPos, 44f * s * g, 44f * s * g, new Color(255, 190, 100, 0) * (0.55f * intensity));
					DrawGlow(sb, shootPos + dirN * (9f * s), 26f * s * g, 14f * s * g, new Color(175, 215, 255, 0) * (0.40f * intensity), angle);
					DrawGlow(sb, shootPos, 18f * s * g, 18f * s * g, new Color(255, 246, 225, 0) * (0.95f * intensity));
				}

				if (rockAlpha > 0.01f && rockScale > 0.04f)
					DrawAsteroidRock(sb, pixel, shootPos, totalTime * 4.0f, rockAlpha, intensity * flicker, rockScale);
				return;
			}

			shootTimer -= dt;
			if (shootTimer <= 0f)
			{
				shootTimer = 24f + (float)fxRand.NextDouble() * 24f;
				// ~2.5s cold approach, then ~11s of sustained burning
				shootMax = shootLife = 14f + (float)fxRand.NextDouble() * 2f;
				fragTimer = 1.0f;

				bool rightToLeft = fxRand.NextDouble() < 0.55;
				float startX = rightToLeft ? w * (0.82f + (float)fxRand.NextDouble() * 0.04f) : w * (0.16f + (float)fxRand.NextDouble() * 0.04f);
				shootVx = (rightToLeft ? -1f : 1f) * w * (0.044f + (float)fxRand.NextDouble() * 0.006f);
				shootAltStart = h * (0.12f + (float)fxRand.NextDouble() * 0.03f);
				shootAltEnd = -h * 0.11f; // dives well into the planet
				shootStartX = startX;
				shootY0 = HorizonY(startX) - shootAltStart;
				shootSlope = 0.035f;
				float endX = startX + shootVx * shootMax;
				float endY = HorizonY(endX) - shootAltEnd;
				shootFall = endY - shootY0 - shootSlope * Math.Abs(endX - startX);
				shootPos = new Vector2(startX, shootY0);
				trailHist.Clear();
			}
		}

		private static void DrawMoon(SpriteBatch sb, int w, int h)
		{
			if (moonTex == null)
				return;

			float size = h * 0.080f;
			Vector2 c = new Vector2(w * 0.865f, h * 0.16f);
			float fade = Math.Min(1f, planetFade * 1.5f);
			DrawGlow(sb, c, size * 1.3f, size * 1.3f, new Color(170, 185, 210, 0) * (0.12f * fade));

			Vector2 origin = new Vector2(MoonSize * 0.5f, MoonSize * 0.5f);
			float scale = size / (float)MoonSize;
			// The sunlit crescent is permanently locked facing towards the Sun (bottom-left),
			// while the Moon's spherical surface (craters, maria) rotates across the sphere in UpdateMoon()
			sb.Draw(moonTex, c, null, Color.White * fade, 0f, origin, scale, SpriteEffects.None, 0f);
		}

		// ═════════════════════════════════════════════════════════════
		//  Deep-Space Easter Egg (Emil's Head / YoRHa Flight Unit / Pod 042)
		// ═════════════════════════════════════════════════════════════

		private static void EnsureEasterEggTextures(GraphicsDevice gd)
		{
			if (emilTex == null || emilTex.IsDisposed)
				emilTex = GenerateEmilTexture(gd);
			if (flightUnitTex == null || flightUnitTex.IsDisposed)
				flightUnitTex = GenerateFlightUnitTexture(gd);
			if (podTex == null || podTex.IsDisposed)
				podTex = GeneratePodTexture(gd);
		}

		private static Texture2D GenerateEmilTexture(GraphicsDevice gd)
		{
			const int S = 32;
			Color[] data = new Color[S * S];
			float cx = 15.5f, cy = 15.5f, R = 12.0f;

			for (int y = 0; y < S; y++)
			{
				for (int x = 0; x < S; x++)
				{
					float dx = x - cx;
					float dy = y - cy;
					float d = (float)Math.Sqrt(dx * dx + dy * dy);

					if (d > R + 0.5f)
					{
						data[y * S + x] = Color.Transparent;
						continue;
					}

					// Dark outer contour outline (1px boundary)
					if (d >= R - 0.9f)
					{
						float edgeA = MathHelper.Clamp(R + 0.5f - d, 0f, 1f);
						data[y * S + x] = new Color(40, 42, 48) * edgeA;
						continue;
					}

					// Spherical surface shading (lit from upper-left SunDir)
					float z = (float)Math.Sqrt(Math.Max(0f, R * R - d * d));
					Vector3 norm = Vector3.Normalize(new Vector3(dx, dy, z));
					Vector3 lightDir = Vector3.Normalize(new Vector3(-0.55f, -0.65f, 0.52f));
					float diff = MathHelper.Clamp(Vector3.Dot(norm, lightDir) * 0.5f + 0.5f, 0f, 1f);
					Color baseBone = Color.Lerp(new Color(135, 140, 150), new Color(245, 246, 250), diff);

					// Eyes: two large, distinct round hollow black sockets
					// Eye 1 at (11.2, 11.2), Eye 2 at (19.8, 11.2), radius 2.6
					float e1d = (float)Math.Sqrt((x - 11.2f) * (x - 11.2f) + (y - 11.2f) * (y - 11.2f));
					float e2d = (float)Math.Sqrt((x - 19.8f) * (x - 19.8f) + (y - 11.2f) * (y - 11.2f));
					if (e1d <= 2.6f || e2d <= 2.6f)
					{
						float ed = Math.Min(e1d, e2d);
						if (ed > 2.0f)
						{
							data[y * S + x] = new Color(30, 32, 38);
						}
						else if (ed > 0.8f && ed < 1.4f)
						{
							data[y * S + x] = new Color(90, 95, 105);
						}
						else
						{
							data[y * S + x] = new Color(18, 18, 22);
						}
						continue;
					}

					// Mouth: wide iconic crescent grin spanning x from 8 to 23
					float mx = (x - cx) / 7.2f;
					if (Math.Abs(mx) <= 1.05f)
					{
						float smileBaseY = 16.5f + 3.2f * (1.0f - mx * mx);
						float distFromSmile = y - smileBaseY;

						if (distFromSmile >= -1.2f && distFromSmile <= 2.2f)
						{
							bool isToothCol = ((x + 1) % 2 == 0) && (x >= 9 && x <= 22);
							bool isUpperTooth = distFromSmile < 0.4f;
							bool isLowerTooth = distFromSmile > 0.8f;

							if (isToothCol && (isUpperTooth || isLowerTooth))
							{
								data[y * S + x] = new Color(245, 245, 248);
							}
							else
							{
								data[y * S + x] = new Color(22, 22, 28);
							}
							continue;
						}
					}

					data[y * S + x] = baseBone;
				}
			}

			Texture2D tex = new Texture2D(gd, S, S);
			tex.SetData(data);
			return tex;
		}

		private static Texture2D GenerateFlightUnitTexture(GraphicsDevice gd)
		{
			const int S = 32;
			Color[] data = new Color[S * S];

			for (int y = 0; y < S; y++)
			{
				for (int x = 0; x < S; x++)
				{
					float fy = y - 15.5f;
					float absY = Math.Abs(fy);

					float maxChordY = (x >= 8 && x <= 27) ? (27f - x) * 0.58f : 0f;
					bool inWings = absY <= maxChordY && x >= 8 && x <= 26;
					bool inFuselage = (absY <= 2.2f && x >= 6 && x <= 28) || (absY <= 1.2f && x >= 26 && x <= 29);
					bool inEngines = (x >= 6 && x <= 14) && (absY >= 3.0f && absY <= 5.5f);

					if (!inWings && !inFuselage && !inEngines)
					{
						data[y * S + x] = Color.Transparent;
						continue;
					}

					if (inEngines)
					{
						data[y * S + x] = (x <= 7) ? new Color(130, 225, 255) : new Color(75, 80, 90);
					}
					else if (inFuselage)
					{
						if (x >= 18 && x <= 23 && absY <= 1.2f)
						{
							data[y * S + x] = new Color(30, 32, 38);
						}
						else
						{
							float shade = (fy < 0f) ? 1.0f : 0.88f;
							data[y * S + x] = new Color((byte)(240 * shade), (byte)(242 * shade), (byte)(248 * shade));
						}
					}
					else
					{
						bool isPanelLine = (x == 16 || (int)(absY + x) % 7 == 0);
						if (isPanelLine)
						{
							data[y * S + x] = new Color(175, 180, 192);
						}
						else
						{
							float shade = (fy < 0f) ? 0.95f : 0.82f;
							data[y * S + x] = new Color((byte)(235 * shade), (byte)(238 * shade), (byte)(244 * shade));
						}
					}
				}
			}

			Texture2D tex = new Texture2D(gd, S, S);
			tex.SetData(data);
			return tex;
		}

		private static Texture2D GeneratePodTexture(GraphicsDevice gd)
		{
			const int S = 24;
			Color[] data = new Color[S * S];
			float cx = 11.5f, cy = 11.5f;

			for (int y = 0; y < S; y++)
			{
				for (int x = 0; x < S; x++)
				{
					float dx = x - cx;
					float dy = y - cy;

					bool inBody = (x >= 7 && x <= 16 && y >= 6 && y <= 17);
					bool inAntenna = (x == 11 && y >= 2 && y <= 5);
					bool inThruster = (x >= 9 && x <= 14 && y == 18) || ((x == 8 || x == 15) && y >= 16 && y <= 19);

					if (!inBody && !inAntenna && !inThruster)
					{
						data[y * S + x] = Color.Transparent;
						continue;
					}

					if (inAntenna)
					{
						data[y * S + x] = (y == 2) ? new Color(255, 230, 150) : new Color(120, 125, 135);
					}
					else if (inThruster)
					{
						data[y * S + x] = new Color(50, 52, 60);
					}
					else
					{
						if (y == 11 && x >= 9 && x <= 14)
						{
							data[y * S + x] = (x == 12) ? new Color(255, 45, 45) : new Color(25, 28, 35);
						}
						else
						{
							float light = MathHelper.Clamp(1.0f - (dx / 6.0f) * 0.25f, 0.75f, 1.0f);
							data[y * S + x] = new Color((byte)(235 * light), (byte)(238 * light), (byte)(245 * light));
						}
					}
				}
			}

			Texture2D tex = new Texture2D(gd, S, S);
			tex.SetData(data);
			return tex;
		}

		private static void DrawEasterEgg(SpriteBatch sb, Texture2D pixel, int w, int h, float dt)
		{
			EnsureEasterEggTextures(Main.instance.GraphicsDevice);
			if (emilTex == null || flightUnitTex == null || podTex == null)
				return;

			if (egg.Active)
			{
				egg.Life -= dt;
				egg.Pos += egg.Vel * dt;
				egg.Rotation += egg.RotSpeed * dt;

				float fadeIn = MathHelper.Clamp((egg.MaxLife - egg.Life) / 2.5f, 0f, 1f);
				float fadeOut = MathHelper.Clamp(egg.Life / 2.5f, 0f, 1f);
				float alpha = egg.Alpha * Math.Min(fadeIn, fadeOut);

				if (egg.Life <= 0f || egg.Pos.X < -120f || egg.Pos.X > w + 120f)
				{
					egg.Active = false;
					return;
				}

				if (alpha <= 0.005f)
					return;

				switch (egg.Type)
				{
					case 0: // Emil's Head (zero-G tumbling smiling sphere with celestial twinkle)
					{
						Vector2 origin = new Vector2(16f, 16f);
						sb.Draw(emilTex, egg.Pos, null, Color.White * alpha, egg.Rotation, origin, egg.Scale, SpriteEffects.None, 0f);

						// Soft subtle blue-white orbital Earth-shine glow around Emil
						DrawGlow(sb, egg.Pos, 22f * egg.Scale, 22f * egg.Scale, new Color(130, 180, 240, 0) * (alpha * 0.20f));

						// Periodic tooth/cheek twinkle sparkle every ~3.5s
						float sparkleTime = totalTime * 1.8f;
						float sparkleWave = (float)Math.Sin(sparkleTime);
						if (sparkleWave > 0.88f)
						{
							float glintA = (sparkleWave - 0.88f) / 0.12f * alpha;
							Vector2 localGlint = new Vector2(5.5f, 4.0f) * egg.Scale;
							float cosR = (float)Math.Cos(egg.Rotation);
							float sinR = (float)Math.Sin(egg.Rotation);
							Vector2 glintPos = egg.Pos + new Vector2(localGlint.X * cosR - localGlint.Y * sinR, localGlint.X * sinR + localGlint.Y * cosR);

							DrawGlow(sb, glintPos, 14f, 14f, new Color(255, 255, 255, 0) * (glintA * 0.9f));
							int spikeLen = (int)(6f * glintA);
							sb.Draw(pixel, new Rectangle((int)glintPos.X - spikeLen, (int)glintPos.Y, spikeLen * 2 + 1, 1), Color.White * glintA);
							sb.Draw(pixel, new Rectangle((int)glintPos.X, (int)glintPos.Y - spikeLen, 1, spikeLen * 2 + 1), Color.White * glintA);
						}
						break;
					}

					case 1: // YoRHa Flight Unit Ho229 (delta-wing cruise with dual cyan ion thruster plumes)
					{
						Vector2 origin = new Vector2(16f, 16f);
						Vector2 dir = egg.Vel.LengthSquared() > 0.001f ? Vector2.Normalize(egg.Vel) : Vector2.UnitX;
						float flightAngle = (float)Math.Atan2(dir.Y, dir.X);

						float thrusterPulse = 0.82f + 0.18f * (float)Math.Sin(totalTime * 32f);
						float plumeLen = 22f * thrusterPulse * egg.Scale;
						float cosA = (float)Math.Cos(flightAngle);
						float sinA = (float)Math.Sin(flightAngle);

						for (int eng = -1; eng <= 1; eng += 2)
						{
							Vector2 engOff = new Vector2(-10f * egg.Scale, eng * 4.5f * egg.Scale);
							Vector2 engPos = egg.Pos + new Vector2(engOff.X * cosA - engOff.Y * sinA, engOff.X * sinA + engOff.Y * cosA);

							DrawGlow(sb, engPos - dir * (plumeLen * 0.45f), plumeLen, 6f * egg.Scale, new Color(100, 210, 255, 0) * (alpha * 0.75f), flightAngle);
							DrawGlow(sb, engPos - dir * (plumeLen * 0.25f), plumeLen * 0.6f, 3.5f * egg.Scale, new Color(230, 250, 255, 0) * (alpha * 0.90f), flightAngle);
						}

						sb.Draw(flightUnitTex, egg.Pos, null, Color.White * alpha, flightAngle, origin, egg.Scale, SpriteEffects.None, 0f);

						// Wingtip navigation beacons: blinking alternately
						float beaconTime = (totalTime * 1.5f) % 1.0f;
						if (beaconTime < 0.25f)
						{
							float bAlpha = alpha * (1f - beaconTime / 0.25f);
							Vector2 portOff = new Vector2(-6f * egg.Scale, -11f * egg.Scale);
							Vector2 stbdOff = new Vector2(-6f * egg.Scale, 11f * egg.Scale);
							Vector2 portPos = egg.Pos + new Vector2(portOff.X * cosA - portOff.Y * sinA, portOff.X * sinA + portOff.Y * cosA);
							Vector2 stbdPos = egg.Pos + new Vector2(stbdOff.X * cosA - stbdOff.Y * sinA, stbdOff.X * sinA + stbdOff.Y * cosA);

							sb.Draw(pixel, new Rectangle((int)portPos.X - 1, (int)portPos.Y - 1, 2, 2), new Color(255, 60, 60) * bAlpha);
							sb.Draw(pixel, new Rectangle((int)stbdPos.X - 1, (int)stbdPos.Y - 1, 2, 2), new Color(80, 255, 140) * bAlpha);
						}
						break;
					}

					case 2: // Pod 042 (tactical support unit with pulsing red optical sensor)
					{
						Vector2 origin = new Vector2(12f, 12f);
						float bobY = (float)Math.Sin(totalTime * 2.2f) * 1.5f;
						Vector2 drawPos = egg.Pos + new Vector2(0f, bobY);
						float tilt = (float)Math.Sin(totalTime * 1.4f) * 0.04f;

						sb.Draw(podTex, drawPos, null, Color.White * alpha, tilt, origin, egg.Scale, SpriteEffects.None, 0f);

						float eyePulse = 0.65f + 0.35f * (float)Math.Sin(totalTime * 5.0f);
						Vector2 eyePos = drawPos + new Vector2(0.5f, -0.5f);
						DrawGlow(sb, eyePos, 8f * egg.Scale * eyePulse, 8f * egg.Scale * eyePulse, new Color(255, 40, 40, 0) * (alpha * 0.85f));

						float puffCycle = (totalTime * 0.8f) % 1.0f;
						if (puffCycle < 0.18f)
						{
							float pAlpha = alpha * (1f - puffCycle / 0.18f);
							Vector2 puffPos = drawPos + new Vector2(0f, 9f * egg.Scale);
							DrawGlow(sb, puffPos, 6f * egg.Scale, 8f * egg.Scale, new Color(140, 200, 255, 0) * (pAlpha * 0.65f));
						}
						break;
					}
				}
				return;
			}

			// Spawning: infrequent rare easter egg sighting across deep space
			eggSpawnTimer -= dt;
			if (eggSpawnTimer <= 0f)
			{
				eggSpawnTimer = 60f + (float)fxRand.NextDouble() * 60f;

				egg.Active = true;
				int roll = fxRand.Next(10);
				egg.Type = roll < 5 ? 0 : (roll < 8 ? 1 : 2); // 50% Emil, 30% Flight Unit, 20% Pod

				bool leftToRight = fxRand.NextDouble() < 0.55;
				float startX = leftToRight ? -40f : (w + 40f);
				float startY = h * (0.07f + (float)fxRand.NextDouble() * 0.16f);
				egg.Pos = new Vector2(startX, startY);

				float transitSpeed = w * (0.024f + (float)fxRand.NextDouble() * 0.006f);
				float vx = leftToRight ? transitSpeed : -transitSpeed;
				float vy = (float)(fxRand.NextDouble() * 2.0 - 1.0) * (w * 0.0022f);
				egg.Vel = new Vector2(vx, vy);

				egg.MaxLife = egg.Life = (w + 100f) / Math.Abs(vx);
				egg.Alpha = 0.95f;

				if (egg.Type == 0) // Emil
				{
					egg.Scale = 0.85f;
					egg.Rotation = (float)(fxRand.NextDouble() * MathHelper.TwoPi);
					egg.RotSpeed = (float)(fxRand.NextDouble() * 0.35 + 0.15) * (leftToRight ? 1f : -1f);
				}
				else if (egg.Type == 1) // Flight Unit
				{
					egg.Scale = 0.75f;
					egg.Rotation = 0f;
					egg.RotSpeed = 0f;
				}
				else // Pod 042
				{
					egg.Scale = 0.80f;
					egg.Rotation = 0f;
					egg.RotSpeed = 0f;
				}
			}
		}

		private static void EnsureBunkerTexture()
		{
			if (bunkerTexture == null)
			{
				try
				{
					bunkerTexture = ModContent.Request<Texture2D>("Augments/Assets/Textures/Bunker", AssetRequestMode.ImmediateLoad);
				}
				catch
				{
				}
			}
		}

		private static void DrawBunker(SpriteBatch sb, int w, int h)
		{
			EnsureBunkerTexture();
			if (bunkerTexture == null || !bunkerTexture.IsLoaded || bunkerTexture.Value == null)
				return;

			Texture2D bunker = bunkerTexture.Value;
			if (bunker.IsDisposed)
				return;

			// Microgravity orbital hover & sway (floating gracefully on the left side of orbit)
			float bobY = (float)Math.Sin(totalTime * 0.45f) * (h * 0.008f);
			float swayX = (float)Math.Cos(totalTime * 0.32f) * (w * 0.005f);
			Vector2 center = new Vector2(w * 0.23f + swayX, h * 0.38f + bobY);

			// Majestic axial rotation in orbit (smooth, synced with orbital drift)
			float rotation = totalTime * 0.038f;

			// Subtle 3D perspective breathing
			float scaleWobble = 1f + 0.015f * (float)Math.Sin(totalTime * 0.5f);
			float baseSize = h * 0.30f;
			float drawSize = baseSize * scaleWobble;
			float scale = drawSize / (float)bunker.Width;

			Vector2 origin = new Vector2(bunker.Width * 0.5f, bunker.Height * 0.5f);

			// 1. Soft atmospheric back-glow / Earth-shine directly behind the station
			float glowSize = drawSize * 0.90f;
			DrawGlow(sb, center, glowSize, glowSize, new Color(90, 150, 220, 0) * (0.14f * planetFade));

			// 2. Main YoRHa Bunker Station Body
			// All station lights (spire beacon, docking bays, hull lamps) are authentically integrated into the 3D model
			Color bunkerColor = new Color(245, 248, 255) * planetFade;
			sb.Draw(bunker, center, null, bunkerColor, rotation, origin, scale, SpriteEffects.None, 0f);
		}

		// Smooth, organic flicker in [0,1]: several incommensurate sines plus a rare "surge".
		private static float SunFlicker(float t, float seed)
		{
			float f = (float)(Math.Sin(t * 1.7 + seed) * 0.40
				+ Math.Sin(t * 3.9 + seed * 2.3) * 0.28
				+ Math.Sin(t * 8.3 + seed * 0.7) * 0.20
				+ Math.Sin(t * 17.1 + seed * 1.9) * 0.12);
			return f * 0.5f + 0.5f;
		}

		private static void DrawSun(SpriteBatch sb, int w, int h)
		{
			if (cachedW <= 0)
				return;

			float sxp = w * 0.10f;
			Vector2 sun = new Vector2(sxp, HorizonY(sxp) - h * 0.004f);
			float fade = 0.35f + 0.65f * planetFade;

			// Flicker channels: slow breathing, medium shimmer, and fast sparkle (atmospheric scintillation)
			float slow = SunFlicker(totalTime * 0.55f, 0.4f);
			float mid = SunFlicker(totalTime * 1.4f, 2.1f);
			float fast = SunFlicker(totalTime * 3.2f, 5.7f);
			float surge = (float)Math.Pow(Math.Max(0f, Math.Sin(totalTime * 0.37) * Math.Sin(totalTime * 0.91 + 1.3)), 3.0);

			float pulse = 1f + 0.05f * (slow - 0.5f) * 2f + 0.025f * (mid - 0.5f) * 2f;
			float coreBright = 0.82f + 0.18f * mid + 0.10f * fast + 0.12f * surge;
			float haloBright = 0.85f + 0.30f * (slow - 0.5f) + 0.15f * surge;

			// Tangent of the horizon at the sun (so the atmospheric flare hugs the curve)
			Vector2 radial = sun - new Vector2(gCx, gCy);
			float tangentAngle = (float)Math.Atan2(radial.X, -radial.Y);

			// Heat-haze: the horizon flare wobbles in length and thickness
			float haze = 1f + 0.06f * (float)Math.Sin(totalTime * 2.3) + 0.04f * (fast - 0.5f);

			// Wide warm bloom
			DrawGlow(sb, sun, h * 0.60f * pulse, h * 0.60f * pulse, new Color(255, 150, 80, 0) * (0.14f * fade * haloBright));
			// Blue Rayleigh streak and warm streak along the limb
			DrawGlow(sb, sun, w * 0.55f * haze, h * 0.045f * (0.9f + 0.2f * mid), new Color(110, 165, 255, 0) * (0.30f * fade * haloBright), tangentAngle);
			DrawGlow(sb, sun, w * 0.30f * haze, h * 0.014f * (0.85f + 0.3f * fast), new Color(255, 175, 95, 0) * (0.65f * fade * coreBright), tangentAngle);
			// Mid halo
			DrawGlow(sb, sun, h * 0.17f * pulse, h * 0.17f * pulse, new Color(255, 205, 160, 0) * (0.42f * fade * haloBright));

			// Corona rays: thin spikes that individually shimmer and slowly rotate
			const int RayCount = 10;
			for (int i = 0; i < RayCount; i++)
			{
				float ang = totalTime * 0.03f + i * (MathHelper.Pi / RayCount) + (i % 2) * 0.11f;
				float rf = SunFlicker(totalTime * (1.1f + i * 0.17f), i * 1.37f);
				float len = h * (0.22f + 0.16f * ((i * 7) % 5) / 4f) * (0.80f + 0.35f * rf);
				float a = (0.050f + 0.060f * rf) * fade * (0.8f + 0.4f * surge);
				DrawGlow(sb, sun, len, h * 0.0022f, new Color(255, 225, 185, 0) * a, ang);
			}

			// Long horizontal anamorphic streak, shimmering
			DrawGlow(sb, sun, w * 0.75f * haze, h * 0.0035f, new Color(170, 200, 255, 0) * (0.40f * fade * (0.75f + 0.5f * fast)));

			// Hot core: bright disc that flickers, with a slightly offset sparkle that jitters
			DrawGlow(sb, sun, h * 0.035f * pulse, h * 0.035f * pulse, new Color(255, 248, 235, 0) * (fade * coreBright));
			Vector2 jitter = new Vector2((fast - 0.5f) * 2.5f, (mid - 0.5f) * 1.5f);
			DrawGlow(sb, sun + jitter, h * 0.020f, h * 0.020f, new Color(255, 240, 215, 0) * (0.35f * fade * fast));
			DrawGlow(sb, sun, h * 0.013f * (0.95f + 0.1f * fast), h * 0.013f * (0.95f + 0.1f * fast), new Color(255, 255, 255, 0) * fade);

			// Lens-flare ghosts along the sun -> screen-centre axis; each flickers out of phase
			Vector2 axis = new Vector2(w * 0.5f, h * 0.5f) - sun;
			float[] dist = { 0.45f, 0.80f, 1.25f, 1.60f, 1.95f };
			float[] rad = { 0.020f, 0.045f, 0.016f, 0.075f, 0.030f };
			Color[] cols =
			{
				new Color(255, 200, 140, 0), new Color(120, 210, 190, 0), new Color(200, 160, 255, 0),
				new Color(110, 160, 230, 0), new Color(255, 180, 120, 0)
			};
			for (int i = 0; i < dist.Length; i++)
			{
				float r = h * rad[i];
				float gf = 0.70f + 0.55f * SunFlicker(totalTime * 2.0f, 3f + i * 1.9f);
				DrawGlow(sb, sun + axis * dist[i], r, r, cols[i] * (0.07f * fade * gf * coreBright));
			}
		}

		private static void DrawGlow(SpriteBatch sb, Vector2 center, float rx, float ry, Color color, float rotation = 0f)
		{
			// Colour alpha of 0 with premultiplied blending = additive light.
			sb.Draw(glowTex, center, null, color, rotation, new Vector2(64f, 64f), new Vector2(rx / 64f, ry / 64f), SpriteEffects.None, 0f);
		}

		// ═════════════════════════════════════════════════════════════
		//  Math helpers / Perlin noise
		// ═════════════════════════════════════════════════════════════

		private static float SS(float e0, float e1, float x)
		{
			float t = MathHelper.Clamp((x - e0) / (e1 - e0), 0f, 1f);
			return t * t * (3f - 2f * t);
		}

		private static float Gaussian(Random r)
		{
			double u1 = 1.0 - r.NextDouble();
			double u2 = r.NextDouble();
			return (float)(Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2));
		}

		private static readonly int[] perm = BuildPerm(1194503);

		private static int[] BuildPerm(int seed)
		{
			var r = new Random(seed);
			int[] p = new int[256];
			for (int i = 0; i < 256; i++) p[i] = i;
			for (int i = 255; i > 0; i--)
			{
				int j = r.Next(i + 1);
				(p[i], p[j]) = (p[j], p[i]);
			}
			int[] pp = new int[512];
			for (int i = 0; i < 512; i++) pp[i] = p[i & 255];
			return pp;
		}

		private static float Fade(float t) => t * t * t * (t * (t * 6f - 15f) + 10f);
		private static float Lerp(float t, float a, float b) => a + t * (b - a);

		private static float Grad(int hash, float x, float y, float z)
		{
			int h = hash & 15;
			float u = h < 8 ? x : y;
			float v = h < 4 ? y : (h == 12 || h == 14 ? x : z);
			return ((h & 1) == 0 ? u : -u) + ((h & 2) == 0 ? v : -v);
		}

		private static float Noise(float x, float y, float z)
		{
			int xi = (int)Math.Floor(x), yi = (int)Math.Floor(y), zi = (int)Math.Floor(z);
			float xf = x - xi, yf = y - yi, zf = z - zi;
			xi &= 255; yi &= 255; zi &= 255;
			float u = Fade(xf), v = Fade(yf), w = Fade(zf);

			int a = perm[xi] + yi, aa = perm[a] + zi, ab = perm[a + 1] + zi;
			int b = perm[xi + 1] + yi, ba = perm[b] + zi, bb = perm[b + 1] + zi;

			return Lerp(w,
				Lerp(v,
					Lerp(u, Grad(perm[aa], xf, yf, zf), Grad(perm[ba], xf - 1, yf, zf)),
					Lerp(u, Grad(perm[ab], xf, yf - 1, zf), Grad(perm[bb], xf - 1, yf - 1, zf))),
				Lerp(v,
					Lerp(u, Grad(perm[aa + 1], xf, yf, zf - 1), Grad(perm[ba + 1], xf - 1, yf, zf - 1)),
					Lerp(u, Grad(perm[ab + 1], xf, yf - 1, zf - 1), Grad(perm[bb + 1], xf - 1, yf - 1, zf - 1))));
		}

		private static float Fbm(float x, float y, float z, int octaves)
		{
			float sum = 0f, amp = 0.5f, freq = 1f;
			for (int i = 0; i < octaves; i++)
			{
				sum += amp * Noise(x * freq, y * freq, z * freq);
				freq *= 2.03f;
				amp *= 0.5f;
			}
			return sum;
		}
	}
}

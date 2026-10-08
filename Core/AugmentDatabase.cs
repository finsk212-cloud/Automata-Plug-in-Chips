using System.Collections.Generic;

namespace Augments
{
    public static class AugmentDatabase
    {
        public static readonly List<Augment> All = new List<Augment>();

        private static readonly Dictionary<string, Augment> ById = new Dictionary<string, Augment>();

        public static void Load()
        {
            All.Clear();
            ById.Clear();

            Register(new BloodletterAugment());
            Register(new SunderAugment());
            Register(new VampiricEdgeAugment());
            Register(new AmbushAugment());
            Register(new RiposteAugment());
            Register(new BerserkerAugment());
            Register(new HeadhunterAugment());
            Register(new LuckyStrikeAugment());
            Register(new MomentumSwingAugment());
            Register(new SecondWindAugment());
            Register(new IronRhythmAugment());
            Register(new SecondPulseAugment());
            Register(new QuickRecoveryAugment());
            Register(new SteadyHeartAugment());
            Register(new PotionRushAugment());
            Register(new GetExcitedAugment());
            Register(new HeartDropAugment());
            Register(new ManaRefluxAugment());
            Register(new ChainLightningAugment());
            Register(new FightOrFlightAugment());
            Register(new IronWillAugment());
            Register(new OverwhelmAugment());
            Register(new SpellEchoAugment());
            Register(new LastStandAugment());
            Register(new FrostTouchAugment());
            Register(new VengeanceAugment());
            Register(new TreasureHunterAugment());
            Register(new OverchargeAugment());
            Register(new CleansingStrikeAugment());
            Register(new ShockwaveAugment());
            Register(new AdaptiveArmorAugment());
            Register(new PhoenixHeartAugment());
            Register(new TimeWarpAugment());
            Register(new LuckyFindAugment());
            Register(new SteadyHandsAugment());
            Register(new MeteorStrikeAugment());
            Register(new SwarmTacticsAugment());
            Register(new FrostboundAugment());
            Register(new MinionMomentumAugment());
            Register(new DarkOmenAugment());
            Register(new DebugFullCritAugment());
            Register(new PlagueBearerAugment());
            Register(new MirrorImageAugment());
            Register(new EternalFlameAugment());
            Register(new OpportunistAugment());
            Register(new ApexPredatorAugment());
            Register(new StormcallerAugment());
            Register(new GuardiansWrathAugment());
            Register(new FinalStandAugment());
            Register(new ReforgersPatienceAugment());
            Register(new SpectralGuardAugment());
            Register(new SharpshooterAugment());
            Register(new PointBlankAugment());
            Register(new ArmorPiercerAugment());
            Register(new ScavengersLuckAugment());
            Register(new QuickfireAugment());
            Register(new VoidStepAugment());
            Register(new RavenousSwarmAugment());
            Register(new DeadeyeAugment());
            Register(new IroncladWillAugment());
            Register(new ApexHunterAugment());
            Register(new InfernosHeartAugment());
            Register(new VitalEchoAugment());
            Register(new EternalLegionAugment());
            Register(new BulwarkAugment());
            Register(new VolatileRoundsAugment());
            Register(new WildCardAugment());
            Register(new FortunesFavorAugment());
            Register(new AvatarOfRageAugment());
            Register(new AvatarOfTheWallAugment());
            Register(new AvatarOfBalanceAugment());
            Register(new TwinStrikeAugment());
            Register(new OverchargeRoundAugment());
            Register(new FesteringWoundsAugment());
            Register(new PiedPiperAugment());
            Register(new TreasureDiverAugment());
            Register(new CriticalSurgeAugment());
            Register(new BrewersInstinctAugment());
            Register(new FeatherfallAugment());
            Register(new MasterAnglerAugment());
            Register(new MendingAuraAugment());
            Register(new DemolitionExpertAugment());
            Register(new GrappleMasterAugment());
            Register(new BaitMasterAugment());
            Register(new SentrysResolveAugment());
            Register(new WhipCrackerAugment());
            Register(new WhipMasterAugment());
            Register(new FocusDirectiveAugment());
            Register(new SentrySalvoAugment());
            Register(new QuickcastAugment());
            Register(new WandDisciplineAugment());
            Register(new CelestialPullAugment());
            Register(new ArcaneSparkAugment());
            Register(new AstralSiphonAugment());
            Register(new FrenziedAssaultAugment());
            Register(new MomentumCrashAugment());
            Register(new TrophyHunterAugment());
            Register(new GodslayerBladeAugment());
            Register(new ArcaneSingularityAugment());
            Register(new QueensSwarmAugment());
            Register(new EchoChamberAugment());
            Register(new RicochetEngineAugment());
            Register(new HuntersPaceAugment());
            Register(new NecromancersCourtAugment());
            Register(new EldritchCovenantAugment());
            Register(new WarGodsTempoAugment());
            Register(new WarCryAugment());
            Register(new IroncladAuraAugment());
            Register(new SwiftnessAuraAugment());
            Register(new ManaWellAugment());
            Register(new SoulLinkAugment());
            Register(new MartyrsResolveAugment());
            Register(new CombatMedicAugment());
            Register(new LastRitesAugment());
            Register(new LifelineAugment());
            Register(new UndyingBondAugment());
            Register(new SoulMartyrAugment());
            Register(new RevitalizingWaveAugment());
            Register(new TauntAugment());
            Register(new CleanseAugment());
            Register(new SwiftReturnAugment());
            Register(new PiercingShotAugment());
            Register(new RapidFireAugment());
            Register(new GlassSightAugment());
            Register(new SharpenedFocusAugment());
            Register(new SuperchargerAugment());
            Register(new AutopilotAugment());
        }

        private static void Register(Augment augment)
        {
            All.Add(augment);
            ById[augment.Id] = augment;
        }

        public static Augment GetById(string id)
        {
            if (id == null)
                return null;

            if (id == "avatar_of_rage") id = "type_b_berserker_protocol";
            else if (id == "avatar_of_the_wall" || id == "type_d_bastion_protocol") id = "type_d_dreadnought_protocol";
            else if (id == "avatar_of_balance") id = "type_s_synchronizer_protocol";

            if (ById.TryGetValue(id, out Augment augment))
                return augment;

            return null;
        }
    }
}

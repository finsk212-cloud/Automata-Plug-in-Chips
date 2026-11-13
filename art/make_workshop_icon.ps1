param([string]$Out)
Add-Type -AssemblyName System.Drawing

$cream = [System.Drawing.Color]::FromArgb(255, 233, 227, 207)
$ink   = [System.Drawing.Color]::FromArgb(255, 34, 32, 27)
$inkSoft = [System.Drawing.Color]::FromArgb(255, 150, 146, 128)

# 5x7 blocky pixel letters
$f = @{
 'A'=@('.###.','#...#','#...#','#####','#...#','#...#','#...#');
 'U'=@('#...#','#...#','#...#','#...#','#...#','#...#','.###.');
 'T'=@('#####','..#..','..#..','..#..','..#..','..#..','..#..');
 'O'=@('.###.','#...#','#...#','#...#','#...#','#...#','.###.');
 'M'=@('#...#','##.##','#.#.#','#...#','#...#','#...#','#...#');
 'P'=@('####.','#...#','#...#','####.','#....','#....','#....');
 'L'=@('#....','#....','#....','#....','#....','#....','#####');
 'G'=@('.###.','#...#','#....','#.###','#...#','#...#','.###.');
 '-'=@('.....','.....','.....','#####','.....','.....','.....');
 'I'=@('#####','..#..','..#..','..#..','..#..','..#..','#####');
 'N'=@('#...#','##..#','#.#.#','#..##','#...#','#...#','#...#');
 'C'=@('.###.','#...#','#....','#....','#....','#...#','.###.');
 'H'=@('#...#','#...#','#...#','#####','#...#','#...#','#...#');
 'S'=@('.####','#....','#....','.###.','....#','....#','####.');
 ' '=@('.....','.....','.....','.....','.....','.....','.....')
}

$bmp = New-Object System.Drawing.Bitmap 512,512
$gfx = [System.Drawing.Graphics]::FromImage($bmp)
$gfx.InterpolationMode = 'NearestNeighbor'; $gfx.PixelOffsetMode = 'Half'; $gfx.SmoothingMode = 'None'
$gfx.Clear($cream)
$bInk = New-Object System.Drawing.SolidBrush $ink

# thin inner frame and heavy corner brackets
$pen = New-Object System.Drawing.Pen $inkSoft, 2
$gfx.DrawRectangle($pen, 34, 34, 443, 443)
$t=8; $len=44; $o=14
foreach($c in @(@($o,$o,1,1),@((512-$o),$o,-1,1),@($o,(512-$o),1,-1),@((512-$o),(512-$o),-1,-1))){
  $x=$c[0]; $y=$c[1]; $dx=$c[2]; $dy=$c[3]
  $gfx.FillRectangle($bInk, [math]::Min($x,$x+$dx*$len), [math]::Min($y,$y+$dy*$t), $len, $t)
  $gfx.FillRectangle($bInk, [math]::Min($x,$x+$dx*$t), [math]::Min($y,$y+$dy*$len), $t, $len)
}

function DrawText($text,$cell,$y){
  $w = $text.Length*6 - 1
  $x0 = [int]((512 - $w*$cell)/2)
  for($i=0;$i -lt $text.Length;$i++){
    $rows=$f[[string]$text[$i]]
    for($r=0;$r -lt 7;$r++){ for($c=0;$c -lt 5;$c++){ if($rows[$r][$c] -eq '#'){ $gfx.FillRectangle($bInk,[int]($x0+($i*6+$c)*$cell),[int]($y+$r*$cell),[int]$cell,[int]$cell) } } }
  }
}

DrawText 'AUTOMATA' 8 188
DrawText 'PLUG-IN CHIPS' 4 290

$bmp.Save($Out,[System.Drawing.Imaging.ImageFormat]::Png)
$gfx.Dispose(); $bmp.Dispose()
"saved $Out"

$ErrorActionPreference = 'Stop'

$root = Join-Path (Get-Location) 'quiz-packs'
$plDir = Join-Path $root 'pl'
$enDir = Join-Path $root 'en'
$czDir = Join-Path $root 'cz'
New-Item -ItemType Directory -Force -Path $plDir | Out-Null
New-Item -ItemType Directory -Force -Path $enDir | Out-Null
New-Item -ItemType Directory -Force -Path $czDir | Out-Null

$systems = @(
    @{ Name='C64'; Maker='Commodore'; Cpu='6510'; Year='1982' },
    @{ Name='C128'; Maker='Commodore'; Cpu='8502'; Year='1985' },
    @{ Name='VIC-20'; Maker='Commodore'; Cpu='6502'; Year='1980' },
    @{ Name='C16'; Maker='Commodore'; Cpu='7501'; Year='1984' },
    @{ Name='Plus/4'; Maker='Commodore'; Cpu='7501'; Year='1984' },
    @{ Name='Atari 800'; Maker='Atari'; Cpu='6502'; Year='1979' },
    @{ Name='Atari XL'; Maker='Atari'; Cpu='6502'; Year='1983' },
    @{ Name='Atari XE'; Maker='Atari'; Cpu='6502'; Year='1985' },
    @{ Name='ZX Spectrum'; Maker='Sinclair'; Cpu='Z80'; Year='1982' },
    @{ Name='ZX81'; Maker='Sinclair'; Cpu='Z80'; Year='1981' },
    @{ Name='Amstrad CPC'; Maker='Amstrad'; Cpu='Z80'; Year='1984' },
    @{ Name='ColecoVision'; Maker='Coleco'; Cpu='Z80'; Year='1982' },
    @{ Name='NES'; Maker='Nintendo'; Cpu='2A03'; Year='1983' },
    @{ Name='Game Boy'; Maker='Nintendo'; Cpu='LR35902'; Year='1989' },
    @{ Name='Apple II'; Maker='Apple'; Cpu='6502'; Year='1977' },
    @{ Name='BBC Micro'; Maker='Acorn'; Cpu='6502'; Year='1981' },
    @{ Name='Oric-1'; Maker='Tangerine'; Cpu='6502'; Year='1983' },
    @{ Name='Enterprise 128'; Maker='Enterprise'; Cpu='Z80'; Year='1985' },
    @{ Name='SAM Coupe'; Maker='MGT'; Cpu='Z80'; Year='1989' },
    @{ Name='Dragon 32'; Maker='Dragon Data'; Cpu='6809'; Year='1982' }
)

$games = @(
    @{ Title='Manic Miner'; Platform='ZX Spectrum'; Publisher='Bug-Byte' },
    @{ Title='Jet Set Willy'; Platform='ZX Spectrum'; Publisher='Software Projects' },
    @{ Title='Elite'; Platform='BBC Micro'; Publisher='Acornsoft' },
    @{ Title='The Last Ninja'; Platform='C64'; Publisher='System 3' },
    @{ Title='Impossible Mission'; Platform='C64'; Publisher='Epyx' },
    @{ Title='Summer Games'; Platform='C64'; Publisher='Epyx' },
    @{ Title='Boulder Dash'; Platform='C64'; Publisher='First Star' },
    @{ Title='Pitfall!'; Platform='Atari 2600'; Publisher='Activision' },
    @{ Title='River Raid'; Platform='Atari 2600'; Publisher='Activision' },
    @{ Title='Ghostbusters'; Platform='C64'; Publisher='Activision' },
    @{ Title='Bruce Lee'; Platform='C64'; Publisher='Datasoft' },
    @{ Title='Zaxxon'; Platform='ColecoVision'; Publisher='Sega' },
    @{ Title='Pac-Man'; Platform='NES'; Publisher='Namco' },
    @{ Title='Super Mario Bros'; Platform='NES'; Publisher='Nintendo' },
    @{ Title='Tetris'; Platform='Game Boy'; Publisher='Nintendo' },
    @{ Title='Prince of Persia'; Platform='Apple II'; Publisher='Broderbund' },
    @{ Title='Lode Runner'; Platform='Apple II'; Publisher='Broderbund' },
    @{ Title='Dizzy'; Platform='ZX Spectrum'; Publisher='Codemasters' },
    @{ Title='Skool Daze'; Platform='ZX Spectrum'; Publisher='Microsphere' },
    @{ Title='Head Over Heels'; Platform='ZX Spectrum'; Publisher='Ocean' }
)

function New-Question {
    param(
        [string]$Id,
        [string]$Question,
        [string]$Correct,
        [string[]]$Pool
    )

    $wrong = $Pool | Where-Object { $_ -ne $Correct } | Get-Random -Count 3
    $all = @($Correct) + $wrong
    $shuffled = $all | Get-Random -Count 4
    $correctIndex = [Array]::IndexOf($shuffled, $Correct)
    $letter = @('A','B','C','D')[$correctIndex]

    return [ordered]@{
        id = $Id
        q = $Question
        a = $shuffled[0]
        b = $shuffled[1]
        c = $shuffled[2]
        d = $shuffled[3]
        correct = $letter
    }
}

$makerPool = ($systems.Maker + @('Commodore','Atari','Sinclair','Nintendo','Apple','Acorn','Amstrad','Coleco','Tangerine','MGT','Dragon Data','Enterprise')) | Select-Object -Unique
$cpuPool = ($systems.Cpu + @('6502','6510','8502','7501','Z80','6809','2A03','LR35902')) | Select-Object -Unique
$yearPool = ($systems.Year + @('1977','1979','1980','1981','1982','1983','1984','1985','1989')) | Select-Object -Unique
$platformPool = ($games.Platform + @('C64','ZX Spectrum','NES','BBC Micro','Apple II','Game Boy','ColecoVision','Atari 2600')) | Select-Object -Unique
$publisherPool = ($games.Publisher + @('Activision','Epyx','Nintendo','Ocean','System 3','Acornsoft','Namco','Codemasters','Broderbund','Datasoft','Sega','First Star','Microsphere','Software Projects','Bug-Byte')) | Select-Object -Unique

$questionsPl = New-Object System.Collections.Generic.List[object]
$questionsEn = New-Object System.Collections.Generic.List[object]
$qid = 1

foreach ($s in $systems) {
    $questionsPl.Add((New-Question -Id ("PL{0:D3}" -f $qid) -Question ("Kto wyprodukowal {0}?" -f $s.Name) -Correct $s.Maker -Pool $makerPool))
    $questionsEn.Add((New-Question -Id ("EN{0:D3}" -f $qid) -Question ("Who made {0}?" -f $s.Name) -Correct $s.Maker -Pool $makerPool))
    $qid++
}
foreach ($s in $systems) {
    $questionPl = "CPU w {0}?" -f $s.Name
    $questionEn = "CPU in {0}?" -f $s.Name

    if ($s.Name -eq 'C128') {
        $questionPl = "CPU w C128 (tryb natywny)?"
        $questionEn = "CPU in C128 (native mode)?"
    }
    elseif ($s.Name -eq 'NES') {
        $questionPl = "CPU w NES (NTSC)?"
        $questionEn = "CPU in NES (NTSC)?"
    }

    $questionsPl.Add((New-Question -Id ("PL{0:D3}" -f $qid) -Question $questionPl -Correct $s.Cpu -Pool $cpuPool))
    $questionsEn.Add((New-Question -Id ("EN{0:D3}" -f $qid) -Question $questionEn -Correct $s.Cpu -Pool $cpuPool))
    $qid++
}
foreach ($s in $systems) {
    $questionPl = "Rok premiery {0}?" -f $s.Name
    $questionEn = "Release year of {0}?" -f $s.Name

    if ($s.Name -eq 'NES') {
        $questionPl = "Rok premiery NES (Japonia)?"
        $questionEn = "Release year of NES (Japan)?"
    }

    $questionsPl.Add((New-Question -Id ("PL{0:D3}" -f $qid) -Question $questionPl -Correct $s.Year -Pool $yearPool))
    $questionsEn.Add((New-Question -Id ("EN{0:D3}" -f $qid) -Question $questionEn -Correct $s.Year -Pool $yearPool))
    $qid++
}
foreach ($g in $games) {
    $questionsPl.Add((New-Question -Id ("PL{0:D3}" -f $qid) -Question ("Glowna platforma {0}?" -f $g.Title) -Correct $g.Platform -Pool $platformPool))
    $questionsEn.Add((New-Question -Id ("EN{0:D3}" -f $qid) -Question ("Main platform for {0}?" -f $g.Title) -Correct $g.Platform -Pool $platformPool))
    $qid++
}
foreach ($g in $games) {
    $questionsPl.Add((New-Question -Id ("PL{0:D3}" -f $qid) -Question ("Wydawca gry {0}?" -f $g.Title) -Correct $g.Publisher -Pool $publisherPool))
    $questionsEn.Add((New-Question -Id ("EN{0:D3}" -f $qid) -Question ("Publisher of {0}?" -f $g.Title) -Correct $g.Publisher -Pool $publisherPool))
    $qid++
}

if ($questionsPl.Count -ne 100 -or $questionsEn.Count -ne 100) {
    throw "Expected 100 questions per language."
}

$plPack = [ordered]@{
    header = [ordered]@{
        id = 'quiz_pl_8bit_v1'
        language = 'pl'
        title = 'Quiz 8-bit PL v1'
        description = 'Krotki quiz o komputerach 8-bit i klasycznych grach. 100 pytan, odpowiedzi A/B/C/D.'
        version = '1.0.0'
        author = 'CsharpBbs'
        createdAt = (Get-Date -Format 'yyyy-MM-dd')
        tags = @('8-bit','retro','gry','commodore','atari')
    }
    questions = $questionsPl
}

$enPack = [ordered]@{
    header = [ordered]@{
        id = 'quiz_en_8bit_v1'
        language = 'en'
        title = '8-bit Quiz EN v1'
        description = 'Short quiz about 8-bit computers and classic games. 100 questions with A/B/C/D answers.'
        version = '1.0.0'
        author = 'CsharpBbs'
        createdAt = (Get-Date -Format 'yyyy-MM-dd')
        tags = @('8-bit','retro','games','commodore','atari')
    }
    questions = $questionsEn
}

$plJson = $plPack | ConvertTo-Json -Depth 8
$enJson = $enPack | ConvertTo-Json -Depth 8

Set-Content -Path (Join-Path $plDir 'quiz_pl_8bit_v1.json') -Value $plJson
Set-Content -Path (Join-Path $enDir 'quiz_en_8bit_v1.json') -Value $enJson

$barejaQuestions = @(
    [ordered]@{ id='BR001'; q='Kto wyrezyserowal film Mis?'; a='Andrzej Wajda'; b='Stanislaw Bareja'; c='Krzysztof Kieslowski'; d='Agnieszka Holland'; correct='B' },
    [ordered]@{ id='BR002'; q='Film z Ryszardem Ochodzkim?'; a='Mis'; b='Seksmisja'; c='Vabank'; d='Psy'; correct='A' },
    [ordered]@{ id='BR003'; q='Haslo o plaszczu jest z filmu?'; a='Rejs'; b='Brunet wieczorowa pora'; c='Mis'; d='Kingsajz'; correct='C' },
    [ordered]@{ id='BR004'; q='Kto zagral Ryszarda Ochodzkiego?'; a='Janusz Gajos'; b='Jerzy Stuhr'; c='Roman Wilhelmi'; d='Stanislaw Tym'; correct='D' },
    [ordered]@{ id='BR005'; q='Poszukiwany, poszukiwana to?'; a='Film Barei'; b='Film Wajdy'; c='Film Machulskiego'; d='Serial TVP'; correct='A' },
    [ordered]@{ id='BR006'; q='Brunet wieczorowa pora to?'; a='Film Barei'; b='Serial kryminalny'; c='Film Koterskiego'; d='Film Smarzowskiego'; correct='A' },
    [ordered]@{ id='BR007'; q='Alternatywy 4 to?'; a='Film kinowy'; b='Serial Barei'; c='Teatr TV'; d='Dokument'; correct='B' },
    [ordered]@{ id='BR008'; q='Kto zagral Stanislawa Aniola?'; a='Roman Wilhelmi'; b='Wojciech Pokora'; c='Jerzy Bonczak'; d='Marian Opania'; correct='A' },
    [ordered]@{ id='BR009'; q='Rok premiery Co mi zrobisz...?'; a='1972'; b='1974'; c='1978'; d='1981'; correct='C' },
    [ordered]@{ id='BR010'; q='Rok premiery Nie ma rozy...?'; a='1974'; b='1976'; c='1978'; d='1983'; correct='A' },
    [ordered]@{ id='BR011'; q='Rok premiery filmu Mis?'; a='1978'; b='1981'; c='1984'; d='1987'; correct='B' },
    [ordered]@{ id='BR012'; q='Ktory tytul NIE jest Barei?'; a='Mis'; b='Zmiennicy'; c='Rejs'; d='Brunet wieczorowa pora'; correct='C' },
    [ordered]@{ id='BR013'; q='Ktory tytul jest dzielem Barei?'; a='Seksmisja'; b='Zmiennicy'; c='Dlugi weekend'; d='Kiler'; correct='B' },
    [ordered]@{ id='BR014'; q='Ktory film wyrezyserowal Bareja?'; a='Psy'; b='Zona dla Australijczyka'; c='Kingsajz'; d='Kogel-mogel'; correct='B' },
    [ordered]@{ id='BR015'; q='Ktory film to Bareja?'; a='Milosc Ci wszystko wybaczy'; b='M jak milosc'; c='Malzenstwo z rozsadku'; d='Noce i dnie'; correct='C' },
    [ordered]@{ id='BR016'; q='Ktory tytul NIE jest Barei?'; a='Czlowiek z marmuru'; b='Mis'; c='Poszukiwany, poszukiwana'; d='Co mi zrobisz, jak mnie zlapiesz'; correct='A' },
    [ordered]@{ id='BR017'; q='Gdzie dzieje sie Alternatywy 4?'; a='Na lotnisku'; b='W bloku osiedlowym'; c='W szkole'; d='W kopalni'; correct='B' },
    [ordered]@{ id='BR018'; q='Nazwisko gospodarza w Alternatywy 4?'; a='Kotek'; b='Balcerek'; c='Aniol'; d='Wolanski'; correct='C' },
    [ordered]@{ id='BR019'; q='Kto gra glowna role w Poszukiwany...?'; a='Wojciech Pokora'; b='Daniel Olbrychski'; c='Zbigniew Zamachowski'; d='Olaf Lubaszenko'; correct='A' },
    [ordered]@{ id='BR020'; q='Zmiennicy to film czy serial?'; a='Film TV'; b='Serial'; c='Dokument'; d='Teledysk'; correct='B' },
    [ordered]@{ id='BR021'; q='Kto wyrezyserowal Zmiennikow?'; a='Krzysztof Zanussi'; b='Jan Jakub Kolski'; c='Stanislaw Bareja'; d='Wojciech Smarzowski'; correct='C' },
    [ordered]@{ id='BR022'; q='Tytul Barei o brunecie to?'; a='Brunet wieczorowa pora'; b='Nocny pociag'; c='Samowolka'; d='Hydrozagadka'; correct='A' },
    [ordered]@{ id='BR023'; q='Tytul Barei ze slowem roza?'; a='Roza i miecz'; b='Nie ma rozy bez ognia'; c='Rozowa pantera'; d='Rozowe lata'; correct='B' },
    [ordered]@{ id='BR024'; q='Serial Barei z numerem w tytule?'; a='07 zglos sie'; b='39 i pol'; c='Alternatywy 4'; d='Daleko od noszy'; correct='C' },
    [ordered]@{ id='BR025'; q='Tytul Barei zaczynajacy sie od Co mi?'; a='Co lubia tygrysy'; b='Co nowego'; c='Co mi zrobisz, jak mnie zlapiesz'; d='Co jest grane'; correct='C' },
    [ordered]@{ id='BR026'; q='Ktory tworca to Bareja?'; a='Stanislaw Bareja'; b='Roman Polanski'; c='Krzysztof Kieslowski'; d='Wladyslaw Pasikowski'; correct='A' },
    [ordered]@{ id='BR027'; q='Mis to film czy serial?'; a='Film'; b='Serial'; c='Teatr TV'; d='Dokument'; correct='A' },
    [ordered]@{ id='BR028'; q='W jakim serialu jest Aniol?'; a='Zmiennicy'; b='Alternatywy 4'; c='Dom'; d='Ekstradycja'; correct='B' },
    [ordered]@{ id='BR029'; q='Rok premiery Poszukiwany, poszukiwana?'; a='1968'; b='1972'; c='1976'; d='1981'; correct='B' },
    [ordered]@{ id='BR030'; q='Rok premiery Brunet wieczorowa pora?'; a='1976'; b='1978'; c='1981'; d='1984'; correct='A' }
)

$barejaPack = [ordered]@{
    header = [ordered]@{
        id = 'quiz_pl_bareja_v1'
        language = 'pl'
        title = 'Quiz: tworczosc Barei'
        description = 'Quiz PL o filmach i serialach Stanislawa Barei. 30 pytan, odpowiedzi A/B/C/D.'
        version = '1.0.0'
        author = 'CsharpBbs'
        createdAt = (Get-Date -Format 'yyyy-MM-dd')
        tags = @('bareja','polskie-kino','film','serial','prl')
    }
    questions = $barejaQuestions
}

$barejaJson = $barejaPack | ConvertTo-Json -Depth 8
Set-Content -Path (Join-Path $plDir 'quiz_pl_bareja_v1.json') -Value $barejaJson

$moviesPlQuestions = @(
    [ordered]@{ id='MVPL001'; q='Kto wyrezyserowal film Jaws?'; a='George Lucas'; b='Steven Spielberg'; c='Ridley Scott'; d='James Cameron'; correct='B' },
    [ordered]@{ id='MVPL002'; q='Rok premiery filmu Jaws?'; a='1975'; b='1977'; c='1981'; d='1984'; correct='A' },
    [ordered]@{ id='MVPL003'; q='Kto gra szeryfa Brody w Jaws?'; a='Roy Scheider'; b='Tom Skerritt'; c='Richard Dreyfuss'; d='Nick Nolte'; correct='A' },
    [ordered]@{ id='MVPL004'; q='W ktorym filmie jest komputer WOPR?'; a='Jaws'; b='WarGames'; c='Terminator'; d='Rocky'; correct='B' },
    [ordered]@{ id='MVPL005'; q='Rok premiery filmu WarGames?'; a='1981'; b='1983'; c='1985'; d='1987'; correct='B' },
    [ordered]@{ id='MVPL006'; q='Kto gra glowna role w WarGames?'; a='Matthew Broderick'; b='Tom Cruise'; c='Ralph Macchio'; d='Sean Astin'; correct='A' },
    [ordered]@{ id='MVPL007'; q='Kto wyrezyserowal film Goonies?'; a='Joe Dante'; b='Richard Donner'; c='Robert Zemeckis'; d='Ivan Reitman'; correct='B' },
    [ordered]@{ id='MVPL008'; q='Rok premiery filmu Goonies?'; a='1982'; b='1984'; c='1985'; d='1988'; correct='C' },
    [ordered]@{ id='MVPL009'; q='Kto napisal scenariusz Goonies?'; a='Chris Columbus'; b='John Hughes'; c='Lawrence Kasdan'; d='Dan Aykroyd'; correct='A' },
    [ordered]@{ id='MVPL010'; q='Rezyser Kosmicznej Odysei?'; a='Stanley Kubrick'; b='Andrei Tarkowski'; c='George Lucas'; d='Ridley Scott'; correct='A' },
    [ordered]@{ id='MVPL011'; q='Tytul angielski Kosmicznej Odysei?'; a='Solaris'; b='2001: A Space Odyssey'; c='Silent Running'; d='Alien'; correct='B' },
    [ordered]@{ id='MVPL012'; q='Rok premiery 2001: A Space Odyssey?'; a='1965'; b='1968'; c='1972'; d='1977'; correct='B' },
    [ordered]@{ id='MVPL013'; q='HAL 9000 jest z filmu?'; a='Star Wars'; b='WarGames'; c='2001: A Space Odyssey'; d='Terminator'; correct='C' },
    [ordered]@{ id='MVPL014'; q='Kto wyrezyserowal Star Wars (1977)?'; a='Irvin Kershner'; b='George Lucas'; c='Richard Marquand'; d='J.J. Abrams'; correct='B' },
    [ordered]@{ id='MVPL015'; q='Rok premiery Star Wars epizod IV?'; a='1975'; b='1977'; c='1980'; d='1983'; correct='B' },
    [ordered]@{ id='MVPL016'; q='Kto gra Luke Skywalkera?'; a='Harrison Ford'; b='Mark Hamill'; c='Carrie Fisher'; d='Alec Guinness'; correct='B' },
    [ordered]@{ id='MVPL017'; q='Kto wyrezyserowal Terminatora?'; a='Paul Verhoeven'; b='James Cameron'; c='Walter Hill'; d='John McTiernan'; correct='B' },
    [ordered]@{ id='MVPL018'; q='Rok premiery filmu Terminator?'; a='1982'; b='1984'; c='1986'; d='1988'; correct='B' },
    [ordered]@{ id='MVPL019'; q='Kto gra Terminatora T-800?'; a='Dolph Lundgren'; b='Arnold Schwarzenegger'; c='Carl Weathers'; d='Rutger Hauer'; correct='B' },
    [ordered]@{ id='MVPL020'; q='Kto gra Sarah Connor?'; a='Sigourney Weaver'; b='Linda Hamilton'; c='Jamie Lee Curtis'; d='Meg Ryan'; correct='B' },
    [ordered]@{ id='MVPL021'; q='Kto wyrezyserowal Ghostbusters?'; a='Ivan Reitman'; b='John Landis'; c='Harold Ramis'; d='Richard Donner'; correct='A' },
    [ordered]@{ id='MVPL022'; q='Rok premiery Ghostbusters?'; a='1981'; b='1984'; c='1986'; d='1989'; correct='B' },
    [ordered]@{ id='MVPL023'; q='Kto gra Petera Venkmana?'; a='Dan Aykroyd'; b='Harold Ramis'; c='Bill Murray'; d='Ernie Hudson'; correct='C' },
    [ordered]@{ id='MVPL024'; q='Kto gra glowna role w Rocky?'; a='Robert De Niro'; b='Sylvester Stallone'; c='Al Pacino'; d='Mickey Rourke'; correct='B' },
    [ordered]@{ id='MVPL025'; q='Rok premiery filmu Rocky?'; a='1974'; b='1976'; c='1978'; d='1980'; correct='B' },
    [ordered]@{ id='MVPL026'; q='Kto wyrezyserowal Rocky?'; a='Martin Scorsese'; b='John G. Avildsen'; c='Francis Ford Coppola'; d='Sam Peckinpah'; correct='B' },
    [ordered]@{ id='MVPL027'; q='Jaki sport uprawia Rocky?'; a='Boks'; b='Zapasy'; c='Football'; d='Hokej'; correct='A' },
    [ordered]@{ id='MVPL028'; q='W jakim miescie mieszka Rocky?'; a='Nowy Jork'; b='Chicago'; c='Filadelfia'; d='Boston'; correct='C' },
    [ordered]@{ id='MVPL029'; q='Ktory film ma haslo The Force?'; a='Rocky'; b='Ghostbusters'; c='Star Wars'; d='Jaws'; correct='C' },
    [ordered]@{ id='MVPL030'; q='Ktory film ma piosenke Ghostbusters?'; a='Terminator'; b='Jaws'; c='WarGames'; d='Ghostbusters'; correct='D' }
)

$moviesCzQuestions = @(
    [ordered]@{ id='MVCZ001'; q='Kdo reziroval film Jaws?'; a='George Lucas'; b='Steven Spielberg'; c='Ridley Scott'; d='James Cameron'; correct='B' },
    [ordered]@{ id='MVCZ002'; q='Rok premiery filmu Jaws?'; a='1975'; b='1977'; c='1981'; d='1984'; correct='A' },
    [ordered]@{ id='MVCZ003'; q='Kdo hral sefpolicistu Brodyho v Jaws?'; a='Roy Scheider'; b='Tom Skerritt'; c='Richard Dreyfuss'; d='Nick Nolte'; correct='A' },
    [ordered]@{ id='MVCZ004'; q='Ve kterem filmu je pocitac WOPR?'; a='Jaws'; b='WarGames'; c='Terminator'; d='Rocky'; correct='B' },
    [ordered]@{ id='MVCZ005'; q='Rok premiery filmu WarGames?'; a='1981'; b='1983'; c='1985'; d='1987'; correct='B' },
    [ordered]@{ id='MVCZ006'; q='Kdo hral hlavni roli ve WarGames?'; a='Matthew Broderick'; b='Tom Cruise'; c='Ralph Macchio'; d='Sean Astin'; correct='A' },
    [ordered]@{ id='MVCZ007'; q='Kdo reziroval film Goonies?'; a='Joe Dante'; b='Richard Donner'; c='Robert Zemeckis'; d='Ivan Reitman'; correct='B' },
    [ordered]@{ id='MVCZ008'; q='Rok premiery filmu Goonies?'; a='1982'; b='1984'; c='1985'; d='1988'; correct='C' },
    [ordered]@{ id='MVCZ009'; q='Kdo napsal scenar k Goonies?'; a='Chris Columbus'; b='John Hughes'; c='Lawrence Kasdan'; d='Dan Aykroyd'; correct='A' },
    [ordered]@{ id='MVCZ010'; q='Kdo reziroval film 2001?'; a='Stanley Kubrick'; b='Andrei Tarkovsky'; c='George Lucas'; d='Ridley Scott'; correct='A' },
    [ordered]@{ id='MVCZ011'; q='Anglicky titul Kosmicke odysey?'; a='Solaris'; b='2001: A Space Odyssey'; c='Silent Running'; d='Alien'; correct='B' },
    [ordered]@{ id='MVCZ012'; q='Rok premiery 2001: A Space Odyssey?'; a='1965'; b='1968'; c='1972'; d='1977'; correct='B' },
    [ordered]@{ id='MVCZ013'; q='HAL 9000 je z filmu?'; a='Star Wars'; b='WarGames'; c='2001: A Space Odyssey'; d='Terminator'; correct='C' },
    [ordered]@{ id='MVCZ014'; q='Kdo reziroval Star Wars (1977)?'; a='Irvin Kershner'; b='George Lucas'; c='Richard Marquand'; d='J.J. Abrams'; correct='B' },
    [ordered]@{ id='MVCZ015'; q='Rok premiery Star Wars epizoda IV?'; a='1975'; b='1977'; c='1980'; d='1983'; correct='B' },
    [ordered]@{ id='MVCZ016'; q='Kdo hral Lukea Skywalkera?'; a='Harrison Ford'; b='Mark Hamill'; c='Carrie Fisher'; d='Alec Guinness'; correct='B' },
    [ordered]@{ id='MVCZ017'; q='Kdo reziroval Terminatora?'; a='Paul Verhoeven'; b='James Cameron'; c='Walter Hill'; d='John McTiernan'; correct='B' },
    [ordered]@{ id='MVCZ018'; q='Rok premiery filmu Terminator?'; a='1982'; b='1984'; c='1986'; d='1988'; correct='B' },
    [ordered]@{ id='MVCZ019'; q='Kdo hral Terminatora T-800?'; a='Dolph Lundgren'; b='Arnold Schwarzenegger'; c='Carl Weathers'; d='Rutger Hauer'; correct='B' },
    [ordered]@{ id='MVCZ020'; q='Kdo hral Sarah Connor?'; a='Sigourney Weaver'; b='Linda Hamilton'; c='Jamie Lee Curtis'; d='Meg Ryan'; correct='B' },
    [ordered]@{ id='MVCZ021'; q='Kdo reziroval Ghostbusters?'; a='Ivan Reitman'; b='John Landis'; c='Harold Ramis'; d='Richard Donner'; correct='A' },
    [ordered]@{ id='MVCZ022'; q='Rok premiery Ghostbusters?'; a='1981'; b='1984'; c='1986'; d='1989'; correct='B' },
    [ordered]@{ id='MVCZ023'; q='Kdo hral Petera Venkmana?'; a='Dan Aykroyd'; b='Harold Ramis'; c='Bill Murray'; d='Ernie Hudson'; correct='C' },
    [ordered]@{ id='MVCZ024'; q='Kdo hral hlavni roli v Rocky?'; a='Robert De Niro'; b='Sylvester Stallone'; c='Al Pacino'; d='Mickey Rourke'; correct='B' },
    [ordered]@{ id='MVCZ025'; q='Rok premiery filmu Rocky?'; a='1974'; b='1976'; c='1978'; d='1980'; correct='B' },
    [ordered]@{ id='MVCZ026'; q='Kdo reziroval Rocky?'; a='Martin Scorsese'; b='John G. Avildsen'; c='Francis Ford Coppola'; d='Sam Peckinpah'; correct='B' },
    [ordered]@{ id='MVCZ027'; q='Jaky sport dela Rocky?'; a='Box'; b='Zapasy'; c='Fotbal'; d='Hokej'; correct='A' },
    [ordered]@{ id='MVCZ028'; q='V jakem meste zije Rocky?'; a='New York'; b='Chicago'; c='Philadelphia'; d='Boston'; correct='C' },
    [ordered]@{ id='MVCZ029'; q='Ktery film ma pojem The Force?'; a='Rocky'; b='Ghostbusters'; c='Star Wars'; d='Jaws'; correct='C' },
    [ordered]@{ id='MVCZ030'; q='Ktery film ma song Ghostbusters?'; a='Terminator'; b='Jaws'; c='WarGames'; d='Ghostbusters'; correct='D' }
)

$moviesEnQuestions = @(
    [ordered]@{ id='MVEN001'; q='Who directed the film Jaws?'; a='George Lucas'; b='Steven Spielberg'; c='Ridley Scott'; d='James Cameron'; correct='B' },
    [ordered]@{ id='MVEN002'; q='Release year of Jaws?'; a='1975'; b='1977'; c='1981'; d='1984'; correct='A' },
    [ordered]@{ id='MVEN003'; q='Who played Chief Brody in Jaws?'; a='Roy Scheider'; b='Tom Skerritt'; c='Richard Dreyfuss'; d='Nick Nolte'; correct='A' },
    [ordered]@{ id='MVEN004'; q='Which film features WOPR?'; a='Jaws'; b='WarGames'; c='Terminator'; d='Rocky'; correct='B' },
    [ordered]@{ id='MVEN005'; q='Release year of WarGames?'; a='1981'; b='1983'; c='1985'; d='1987'; correct='B' },
    [ordered]@{ id='MVEN006'; q='Lead actor in WarGames?'; a='Matthew Broderick'; b='Tom Cruise'; c='Ralph Macchio'; d='Sean Astin'; correct='A' },
    [ordered]@{ id='MVEN007'; q='Who directed The Goonies?'; a='Joe Dante'; b='Richard Donner'; c='Robert Zemeckis'; d='Ivan Reitman'; correct='B' },
    [ordered]@{ id='MVEN008'; q='Release year of The Goonies?'; a='1982'; b='1984'; c='1985'; d='1988'; correct='C' },
    [ordered]@{ id='MVEN009'; q='Who wrote The Goonies script?'; a='Chris Columbus'; b='John Hughes'; c='Lawrence Kasdan'; d='Dan Aykroyd'; correct='A' },
    [ordered]@{ id='MVEN010'; q='Who directed 2001?'; a='Stanley Kubrick'; b='Andrei Tarkovsky'; c='George Lucas'; d='Ridley Scott'; correct='A' },
    [ordered]@{ id='MVEN011'; q='English title of Kosmiczna Odyseja?'; a='Solaris'; b='2001: A Space Odyssey'; c='Silent Running'; d='Alien'; correct='B' },
    [ordered]@{ id='MVEN012'; q='Release year of 2001?'; a='1965'; b='1968'; c='1972'; d='1977'; correct='B' },
    [ordered]@{ id='MVEN013'; q='HAL 9000 appears in which film?'; a='Star Wars'; b='WarGames'; c='2001: A Space Odyssey'; d='Terminator'; correct='C' },
    [ordered]@{ id='MVEN014'; q='Who directed Star Wars (1977)?'; a='Irvin Kershner'; b='George Lucas'; c='Richard Marquand'; d='J.J. Abrams'; correct='B' },
    [ordered]@{ id='MVEN015'; q='Release year of Star Wars IV?'; a='1975'; b='1977'; c='1980'; d='1983'; correct='B' },
    [ordered]@{ id='MVEN016'; q='Who played Luke Skywalker?'; a='Harrison Ford'; b='Mark Hamill'; c='Carrie Fisher'; d='Alec Guinness'; correct='B' },
    [ordered]@{ id='MVEN017'; q='Who directed The Terminator?'; a='Paul Verhoeven'; b='James Cameron'; c='Walter Hill'; d='John McTiernan'; correct='B' },
    [ordered]@{ id='MVEN018'; q='Release year of Terminator?'; a='1982'; b='1984'; c='1986'; d='1988'; correct='B' },
    [ordered]@{ id='MVEN019'; q='Who played T-800?'; a='Dolph Lundgren'; b='Arnold Schwarzenegger'; c='Carl Weathers'; d='Rutger Hauer'; correct='B' },
    [ordered]@{ id='MVEN020'; q='Who played Sarah Connor?'; a='Sigourney Weaver'; b='Linda Hamilton'; c='Jamie Lee Curtis'; d='Meg Ryan'; correct='B' },
    [ordered]@{ id='MVEN021'; q='Who directed Ghostbusters?'; a='Ivan Reitman'; b='John Landis'; c='Harold Ramis'; d='Richard Donner'; correct='A' },
    [ordered]@{ id='MVEN022'; q='Release year of Ghostbusters?'; a='1981'; b='1984'; c='1986'; d='1989'; correct='B' },
    [ordered]@{ id='MVEN023'; q='Who played Peter Venkman?'; a='Dan Aykroyd'; b='Harold Ramis'; c='Bill Murray'; d='Ernie Hudson'; correct='C' },
    [ordered]@{ id='MVEN024'; q='Who played the lead in Rocky?'; a='Robert De Niro'; b='Sylvester Stallone'; c='Al Pacino'; d='Mickey Rourke'; correct='B' },
    [ordered]@{ id='MVEN025'; q='Release year of Rocky?'; a='1974'; b='1976'; c='1978'; d='1980'; correct='B' },
    [ordered]@{ id='MVEN026'; q='Who directed Rocky?'; a='Martin Scorsese'; b='John G. Avildsen'; c='Francis Ford Coppola'; d='Sam Peckinpah'; correct='B' },
    [ordered]@{ id='MVEN027'; q='Which sport does Rocky do?'; a='Boxing'; b='Wrestling'; c='Football'; d='Hockey'; correct='A' },
    [ordered]@{ id='MVEN028'; q='What city does Rocky live in?'; a='New York'; b='Chicago'; c='Philadelphia'; d='Boston'; correct='C' },
    [ordered]@{ id='MVEN029'; q='Which film features The Force?'; a='Rocky'; b='Ghostbusters'; c='Star Wars'; d='Jaws'; correct='C' },
    [ordered]@{ id='MVEN030'; q='Which film has Ghostbusters song?'; a='Terminator'; b='Jaws'; c='WarGames'; d='Ghostbusters'; correct='D' }
)

$moviesPlPack = [ordered]@{
    header = [ordered]@{
        id = 'quiz_pl_cult_movies_v1'
        language = 'pl'
        title = 'Quiz: kultowe filmy SF i akcja'
        description = 'Quiz PL o filmach WarGames, Goonies, Jaws, 2001, Star Wars, Terminator, Ghostbusters i Rocky.'
        version = '1.0.0'
        author = 'CsharpBbs'
        createdAt = (Get-Date -Format 'yyyy-MM-dd')
        tags = @('film','kino','scifi','akcja','retro')
    }
    questions = $moviesPlQuestions
}

$moviesCzPack = [ordered]@{
    header = [ordered]@{
        id = 'quiz_cz_cult_movies_v1'
        language = 'cz'
        title = 'Kviz: kultovni filmy SF a akce'
        description = 'Kviz CZ o filmech WarGames, Goonies, Jaws, 2001, Star Wars, Terminator, Ghostbusters a Rocky.'
        version = '1.0.0'
        author = 'CsharpBbs'
        createdAt = (Get-Date -Format 'yyyy-MM-dd')
        tags = @('film','kino','scifi','akce','retro')
    }
    questions = $moviesCzQuestions
}

$moviesEnPack = [ordered]@{
    header = [ordered]@{
        id = 'quiz_en_cult_movies_v1'
        language = 'en'
        title = 'Quiz: cult sci-fi and action films'
        description = 'Quiz EN about WarGames, Goonies, Jaws, 2001, Star Wars, Terminator, Ghostbusters and Rocky.'
        version = '1.0.0'
        author = 'CsharpBbs'
        createdAt = (Get-Date -Format 'yyyy-MM-dd')
        tags = @('film','cinema','scifi','action','retro')
    }
    questions = $moviesEnQuestions
}

$moviesPlJson = $moviesPlPack | ConvertTo-Json -Depth 8
$moviesCzJson = $moviesCzPack | ConvertTo-Json -Depth 8
$moviesEnJson = $moviesEnPack | ConvertTo-Json -Depth 8

Set-Content -Path (Join-Path $plDir 'quiz_pl_cult_movies_v1.json') -Value $moviesPlJson
Set-Content -Path (Join-Path $czDir 'quiz_cz_cult_movies_v1.json') -Value $moviesCzJson
Set-Content -Path (Join-Path $enDir 'quiz_en_cult_movies_v1.json') -Value $moviesEnJson

Write-Output "Generated PL: $($questionsPl.Count), EN: $($questionsEn.Count), BAREJA_PL: $($barejaQuestions.Count), MOVIES_PL: $($moviesPlQuestions.Count), MOVIES_EN: $($moviesEnQuestions.Count), MOVIES_CZ: $($moviesCzQuestions.Count)"

# ---------------------------------------------------------------------------
# Override "cult movies" packs with expanded 100-question sets.
# Scope requested by user: mostly 1970-1999 movies + explicit Forbidden Planet.
# ---------------------------------------------------------------------------

$movieCatalog = @(
    @{ Title='Jaws'; Year='1975'; Director='Steven Spielberg'; Lead='Roy Scheider'; ClueEn='Amity shark'; CluePl='rekin z Amity'; ClueCz='zralok z Amity' },
    @{ Title='WarGames'; Year='1983'; Director='John Badham'; Lead='Matthew Broderick'; ClueEn='computer WOPR'; CluePl='komputer WOPR'; ClueCz='pocitac WOPR' },
    @{ Title='The Goonies'; Year='1985'; Director='Richard Donner'; Lead='Sean Astin'; ClueEn='One-Eyed Willy map'; CluePl='mapa One-Eyed Willy'; ClueCz='mapa One-Eyed Willy' },
    @{ Title='2001: A Space Odyssey'; Year='1968'; Director='Stanley Kubrick'; Lead='Keir Dullea'; ClueEn='HAL 9000'; CluePl='HAL 9000'; ClueCz='HAL 9000' },
    @{ Title='Star Wars'; Year='1977'; Director='George Lucas'; Lead='Mark Hamill'; ClueEn='The Force'; CluePl='Moc'; ClueCz='Sila' },
    @{ Title='The Terminator'; Year='1984'; Director='James Cameron'; Lead='Arnold Schwarzenegger'; ClueEn='T-800'; CluePl='T-800'; ClueCz='T-800' },
    @{ Title='Ghostbusters'; Year='1984'; Director='Ivan Reitman'; Lead='Bill Murray'; ClueEn='proton pack'; CluePl='plecak protonowy'; ClueCz='protonovy batoh' },
    @{ Title='Rocky'; Year='1976'; Director='John G. Avildsen'; Lead='Sylvester Stallone'; ClueEn='Apollo Creed'; CluePl='Apollo Creed'; ClueCz='Apollo Creed' },
    @{ Title='Forbidden Planet'; Year='1956'; Director='Fred M. Wilcox'; Lead='Leslie Nielsen'; ClueEn='Robby the Robot'; CluePl='Robby Robot'; ClueCz='Robby Robot' },
    @{ Title='Alien'; Year='1979'; Director='Ridley Scott'; Lead='Sigourney Weaver'; ClueEn='Nostromo'; CluePl='Nostromo'; ClueCz='Nostromo' },
    @{ Title='Blade Runner'; Year='1982'; Director='Ridley Scott'; Lead='Harrison Ford'; ClueEn='replicants'; CluePl='replikanci'; ClueCz='replikanti' },
    @{ Title='Back to the Future'; Year='1985'; Director='Robert Zemeckis'; Lead='Michael J. Fox'; ClueEn='DeLorean'; CluePl='DeLorean'; ClueCz='DeLorean' },
    @{ Title='The Empire Strikes Back'; Year='1980'; Director='Irvin Kershner'; Lead='Mark Hamill'; ClueEn='Hoth'; CluePl='Hoth'; ClueCz='Hoth' },
    @{ Title='Raiders of the Lost Ark'; Year='1981'; Director='Steven Spielberg'; Lead='Harrison Ford'; ClueEn='Ark of the Covenant'; CluePl='Arka Przymierza'; ClueCz='Archa umluvy' },
    @{ Title='Predator'; Year='1987'; Director='John McTiernan'; Lead='Arnold Schwarzenegger'; ClueEn='jungle hunter'; CluePl='lowca z dzungli'; ClueCz='lovec z dzungle' },
    @{ Title='RoboCop'; Year='1987'; Director='Paul Verhoeven'; Lead='Peter Weller'; ClueEn='OCP Detroit'; CluePl='OCP Detroit'; ClueCz='OCP Detroit' },
    @{ Title='The Matrix'; Year='1999'; Director='Wachowski Sisters'; Lead='Keanu Reeves'; ClueEn='red pill'; CluePl='czerwona pigulka'; ClueCz='cervena pilulka' },
    @{ Title='Jurassic Park'; Year='1993'; Director='Steven Spielberg'; Lead='Sam Neill'; ClueEn='cloned dinosaurs'; CluePl='klonowane dinozaury'; ClueCz='klonovani dinosauri' },
    @{ Title='Total Recall'; Year='1990'; Director='Paul Verhoeven'; Lead='Arnold Schwarzenegger'; ClueEn='Mars memory'; CluePl='wspomnienia Marsa'; ClueCz='vzpominky na Mars' },
    @{ Title='The Fifth Element'; Year='1997'; Director='Luc Besson'; Lead='Bruce Willis'; ClueEn='Leeloo'; CluePl='Leeloo'; ClueCz='Leeloo' }
)

function New-MovieQuestions {
    param(
        [string]$Language,
        [string]$IdPrefix,
        [object[]]$Movies
    )

    $directorPool = $Movies | ForEach-Object { $_.Director } | Select-Object -Unique
    $yearPool = $Movies | ForEach-Object { $_.Year } | Select-Object -Unique
    $leadPool = $Movies | ForEach-Object { $_.Lead } | Select-Object -Unique
    $titlePool = $Movies | ForEach-Object { $_.Title } | Select-Object -Unique

    $questions = New-Object System.Collections.Generic.List[object]
    $id = 1

    foreach ($m in $Movies) {
        switch ($Language) {
            'pl' {
                $qDirector = "Kto wyrezyserowal {0}?" -f $m.Title
                $qYear = "Rok premiery {0}?" -f $m.Title
                $qLead = "Glowny aktor w {0}?" -f $m.Title
                $qClue = "Film z motywem: {0}?" -f $m.CluePl
                $qPremiere = "Rok swiatowej premiery {0}?" -f $m.Title
            }
            'cz' {
                $qDirector = "Kdo reziroval {0}?" -f $m.Title
                $qYear = "Rok premiery {0}?" -f $m.Title
                $qLead = "Hlavni herec ve {0}?" -f $m.Title
                $qClue = "Film s motivem: {0}?" -f $m.ClueCz
                $qPremiere = "Rok svetove premiery {0}?" -f $m.Title
            }
            default {
                $qDirector = "Who directed {0}?" -f $m.Title
                $qYear = "Release year of {0}?" -f $m.Title
                $qLead = "Lead actor in {0}?" -f $m.Title
                $qClue = "Which film has: {0}?" -f $m.ClueEn
                $qPremiere = "World premiere year of {0}?" -f $m.Title
            }
        }

        $questions.Add((New-Question -Id ("{0}{1:D3}" -f $IdPrefix, $id) -Question $qDirector -Correct $m.Director -Pool $directorPool))
        $id++
        $questions.Add((New-Question -Id ("{0}{1:D3}" -f $IdPrefix, $id) -Question $qYear -Correct $m.Year -Pool $yearPool))
        $id++
        $questions.Add((New-Question -Id ("{0}{1:D3}" -f $IdPrefix, $id) -Question $qLead -Correct $m.Lead -Pool $leadPool))
        $id++
        $questions.Add((New-Question -Id ("{0}{1:D3}" -f $IdPrefix, $id) -Question $qClue -Correct $m.Title -Pool $titlePool))
        $id++
        $questions.Add((New-Question -Id ("{0}{1:D3}" -f $IdPrefix, $id) -Question $qPremiere -Correct $m.Year -Pool $yearPool))
        $id++
    }

    return $questions
}

$moviesPlQuestions = New-MovieQuestions -Language 'pl' -IdPrefix 'MVPL' -Movies $movieCatalog
$moviesEnQuestions = New-MovieQuestions -Language 'en' -IdPrefix 'MVEN' -Movies $movieCatalog
$moviesCzQuestions = New-MovieQuestions -Language 'cz' -IdPrefix 'MVCZ' -Movies $movieCatalog

if ($moviesPlQuestions.Count -ne 100 -or $moviesEnQuestions.Count -ne 100 -or $moviesCzQuestions.Count -ne 100) {
    throw "Expected 100 cult movie questions per language."
}

$moviesPlPack = [ordered]@{
    header = [ordered]@{
        id = 'quiz_pl_cult_movies_v1'
        language = 'pl'
        title = 'Kultowe filmy'
        description = 'Kultowe filmy'
        version = '1.1.0'
        author = 'CsharpBbs'
        createdAt = (Get-Date -Format 'yyyy-MM-dd')
        tags = @('film','kino','scifi','akcja','retro')
    }
    questions = $moviesPlQuestions
}

$moviesEnPack = [ordered]@{
    header = [ordered]@{
        id = 'quiz_en_cult_movies_v1'
        language = 'en'
        title = 'Quiz: cult films 70-99 + Forbidden Planet'
        description = '100 EN questions about cult movies from 1970-1999 plus Forbidden Planet.'
        version = '1.1.0'
        author = 'CsharpBbs'
        createdAt = (Get-Date -Format 'yyyy-MM-dd')
        tags = @('film','cinema','scifi','action','retro')
    }
    questions = $moviesEnQuestions
}

$moviesCzPack = [ordered]@{
    header = [ordered]@{
        id = 'quiz_cz_cult_movies_v1'
        language = 'cz'
        title = 'Kviz: kultovni filmy 70-99 + Forbidden Planet'
        description = '100 CZ otazek o kultovnich filmech 1970-1999 plus Forbidden Planet.'
        version = '1.1.0'
        author = 'CsharpBbs'
        createdAt = (Get-Date -Format 'yyyy-MM-dd')
        tags = @('film','kino','scifi','akce','retro')
    }
    questions = $moviesCzQuestions
}

Set-Content -Path (Join-Path $plDir 'quiz_pl_cult_movies_v1.json') -Value ($moviesPlPack | ConvertTo-Json -Depth 8)
Set-Content -Path (Join-Path $enDir 'quiz_en_cult_movies_v1.json') -Value ($moviesEnPack | ConvertTo-Json -Depth 8)
Set-Content -Path (Join-Path $czDir 'quiz_cz_cult_movies_v1.json') -Value ($moviesCzPack | ConvertTo-Json -Depth 8)

Write-Output "Expanded cult movies packs: PL=$($moviesPlQuestions.Count), EN=$($moviesEnQuestions.Count), CZ=$($moviesCzQuestions.Count)"

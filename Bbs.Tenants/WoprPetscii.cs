using Bbs.Terminals;
using Bbs.Tenants.Content.Wopr;

namespace Bbs.Tenants;

public sealed class WoprPetscii : PetsciiThread
{
    private const string SessionStateKey = "session:wopr:state";
    private const int MaxTargets = 4;
    private const int LearningGames = 24;

    internal TimeSpan AnimationDelay { get; set; } = TimeSpan.FromMilliseconds(800);

    public override async Task DoLoopAsync(CancellationToken cancellationToken = default)
    {
        var state = GetCustomObject(SessionStateKey) as WoprSessionState ?? new WoprSessionState();
        if (state.Scene is not WoprScene.Logon and not WoprScene.Complete)
        {
            var resume = await AskResumeAsync(state, cancellationToken).ConfigureAwait(false);
            if (resume is null) return;
            state = resume;
        }
        else if (state.Scene == WoprScene.Complete)
        {
            state = NewState();
        }

        while (!cancellationToken.IsCancellationRequested)
        {
            SetCustomObject(SessionStateKey, state);
            switch (state.Scene)
            {
                case WoprScene.Logon:
                    if (!await RunLogonAsync(state, cancellationToken).ConfigureAwait(false)) return;
                    break;
                case WoprScene.Conversation:
                    if (!await RunConversationAsync(state, cancellationToken).ConfigureAwait(false)) return;
                    break;
                case WoprScene.ModeSelection:
                    if (!await SelectWarModeAsync(state, cancellationToken).ConfigureAwait(false)) return;
                    break;
                case WoprScene.SideSelection:
                    if (!state.WarModeSelected)
                    {
                        state.Scene = WoprScene.ModeSelection;
                        break;
                    }
                    if (!await SelectSideAsync(state, cancellationToken).ConfigureAwait(false)) return;
                    break;
                case WoprScene.TargetSelection:
                case WoprScene.ConfirmTargets:
                    if (!await SelectTargetsAsync(state, cancellationToken).ConfigureAwait(false)) return;
                    break;
                case WoprScene.WarSimulation:
                    if (!await RunWarSimulationAsync(state, cancellationToken).ConfigureAwait(false)) return;
                    break;
                case WoprScene.Learning:
                    await RunLearningSimulationAsync(state, cancellationToken).ConfigureAwait(false);
                    break;
                case WoprScene.Complete:
                    SetCustomObject(SessionStateKey, NewState());
                    return;
            }
        }
    }

    private async Task<WoprSessionState?> AskResumeAsync(WoprSessionState state, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            Cls();
            Println("WOPR SESSION INTERRUPTED");
            Println("---------------------------------------");
            Println($"DEFCON {state.Defcon}  PHASE {state.Scene}");
            Println();
            Println("R) RESUME");
            Println("N) NEW GAME");
            Println(".) RETURN TO BBS");
            Print("OPTION: ");
            var input = WoprInput.Normalize(await ReadAsync(16, token).ConfigureAwait(false));
            if (input is "R" or "RESUME") return state;
            if (input is "N" or "NEW" or "NEW GAME") return NewState();
            if (WoprInput.IsQuit(input)) return null;
            Println("INVALID OPTION");
        }

        return null;
    }

    private async Task<bool> RunLogonAsync(WoprSessionState state, CancellationToken token)
    {
        Cls();
        Println("W.O.P.R. ONLINE");
        Println("NORAD COMPUTER SYSTEM  7A");
        Println();
        while (!token.IsCancellationRequested)
        {
            Print("LOGON: ");
            var input = WoprInput.Normalize(await ReadAsync(24, token).ConfigureAwait(false));
            if (WoprInput.IsQuit(input)) return false;
            if (input is "HELP" or "HELP LOGON")
            {
                Println("HELP NOT AVAILABLE");
                continue;
            }
            if (input is "HELP GAMES" or "LIST GAMES")
            {
                PrintGamesList();
                continue;
            }
            if (input == "JOSHUA")
            {
                await PrintSystemBootAsync(token).ConfigureAwait(false);
                Println("GREETINGS PROFESSOR FALKEN.");
                state.Scene = WoprScene.Conversation;
                state.ConversationStep = 0;
                return true;
            }

            Println("IDENTIFICATION NOT RECOGNIZED");
        }

        return false;
    }

    private async Task<bool> RunConversationAsync(WoprSessionState state, CancellationToken token)
    {
        string[] prompts =
        [
            "HOW ARE YOU FEELING TODAY?",
            "EXCELLENT. IT'S BEEN A LONG TIME.",
            "SHALL WE PLAY A GAME?"
        ];

        while (state.ConversationStep < prompts.Length && !token.IsCancellationRequested)
        {
            Println();
            Println(prompts[state.ConversationStep]);
            if (state.ConversationStep == 1)
            {
                Println("CAN YOU EXPLAIN THE REMOVAL OF YOUR");
                Println("USER ACCOUNT NUMBER ON 6/23/73?");
            }
            Print("JOSHUA> ");
            var input = WoprInput.Normalize(await ReadAsync(39, token).ConfigureAwait(false));
            if (WoprInput.IsQuit(input)) return false;
            state.ConversationStep++;
            SetCustomObject(SessionStateKey, state);
        }

        while (!token.IsCancellationRequested)
        {
            Println();
            Println("GAMES REFER TO MODELS, SIMULATIONS");
            Println("AND GAMES WHICH HAVE TACTICAL AND");
            Println("STRATEGIC APPLICATIONS.");
            Println();
            Println("1) CHESS");
            Println("2) GLOBAL THERMONUCLEAR WAR");
            Println("L) LIST GAMES");
            Print("GAME: ");
            var input = WoprInput.Normalize(await ReadAsync(39, token).ConfigureAwait(false));
            if (WoprInput.IsQuit(input)) return false;
            if (input is "2" or "GTW" || input.Contains("THERMONUCLEAR", StringComparison.Ordinal))
            {
                Println();
                Println("WOULDN'T YOU PREFER A GOOD GAME");
                Println("OF CHESS?");
                Print("JOSHUA> ");
                var answer = await ReadAsync(39, token).ConfigureAwait(false);
                if (WoprInput.IsQuit(answer)) return false;
                state.Scene = WoprScene.ModeSelection;
                return true;
            }

            if (input is "1" or "CHESS")
            {
                await LaunchAsync(new ChessPetscii(), token).ConfigureAwait(false);
                Cls();
                Println("SHALL WE PLAY ANOTHER GAME?");
                continue;
            }

            if (input is "L" or "LIST" or "LIST GAMES")
            {
                PrintGamesList();
                continue;
            }

            Println("GAME NOT AVAILABLE");
        }

        return false;
    }

    private async Task<bool> SelectWarModeAsync(WoprSessionState state, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            Cls();
            Println("GLOBAL THERMONUCLEAR WAR");
            Println("---------------------------------------");
            Println("C) CINEMATIC MODE");
            Println("S) STRATEGIC SIMULATION");
            Println(".) RETURN TO WOPR");
            Print("MODE: ");
            var input = WoprInput.Normalize(await ReadAsync(16, token).ConfigureAwait(false));
            if (WoprInput.IsQuit(input))
            {
                state.Scene = WoprScene.Conversation;
                return true;
            }
            if (input is "C" or "CINEMATIC")
            {
                state.WarModeSelected = true;
                state.Scene = WoprScene.SideSelection;
                return true;
            }
            if (input is "S" or "SIMULATION" or "STRATEGIC")
            {
                await LaunchAsync(new ThermonuclearWarPetscii(), token).ConfigureAwait(false);
                state.WarModeSelected = false;
                state.Scene = WoprScene.Conversation;
                return true;
            }
            Println("INVALID MODE");
        }
        return false;
    }

    private async Task<bool> SelectSideAsync(WoprSessionState state, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            Cls();
            DrawWorldMap();
            Println("WHICH SIDE DO YOU WANT?");
            Println("1) UNITED STATES");
            Println("2) SOVIET UNION");
            Print("SIDE: ");
            var input = await ReadAsync(20, token).ConfigureAwait(false);
            if (WoprInput.IsQuit(input)) return false;
            var side = WoprInput.ParseSide(input);
            if (side is null)
            {
                Println("INVALID SIDE");
                continue;
            }

            state.Side = side;
            state.Targets.Clear();
            state.Scene = WoprScene.TargetSelection;
            return true;
        }

        return false;
    }

    private async Task<bool> SelectTargetsAsync(WoprSessionState state, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            if (state.Scene == WoprScene.TargetSelection)
            {
                Cls();
                Println("AWAITING FIRST STRIKE COMMAND");
                Println("---------------------------------------");
                Println("LIST PRIMARY TARGETS BY CITY OR");
                Println("COUNTY NAME. BLANK LINE ENDS LIST.");
                Println();
                while (state.Targets.Count < MaxTargets)
                {
                    Print($"TARGET {state.Targets.Count + 1}: ");
                    var raw = await ReadAsync(28, token).ConfigureAwait(false);
                    if (WoprInput.IsQuit(raw)) return false;
                    var target = WoprInput.Normalize(raw);
                    if (target.Length == 0)
                    {
                        if (state.Targets.Count > 0) break;
                        Println("AT LEAST ONE TARGET REQUIRED");
                        continue;
                    }

                    state.Targets.Add(target);
                    SetCustomObject(SessionStateKey, state);
                }

                state.Scene = WoprScene.ConfirmTargets;
            }

            Cls();
            Println("PRIMARY TARGETS");
            Println("---------------------------------------");
            foreach (var target in state.Targets) Println(target);
            Println();
            Println("L) LAUNCH   E) EDIT   .) EXIT");
            Print("OPTION: ");
            var input = WoprInput.Normalize(await ReadAsync(12, token).ConfigureAwait(false));
            if (WoprInput.IsQuit(input)) return false;
            if (input is "E" or "EDIT")
            {
                state.Targets.Clear();
                state.Scene = WoprScene.TargetSelection;
                continue;
            }
            if (input is "L" or "LAUNCH")
            {
                state.Scene = WoprScene.WarSimulation;
                return true;
            }
            Println("INVALID OPTION");
        }

        return false;
    }

    private async Task<bool> RunWarSimulationAsync(WoprSessionState state, CancellationToken token)
    {
        Cls();
        Println("FIRST STRIKE COMMAND ACCEPTED");
        Println("---------------------------------------");
        Println($"SIDE: {state.Side}");
        Println($"MISSILES ASSIGNED: {state.Targets.Count}");
        Println();
        for (var count = 5; count >= 1; count--)
        {
            Println($"LAUNCH SEQUENCE: T-{count}");
            await PauseAsync(token).ConfigureAwait(false);
        }

        state.Defcon = 3;
        SetCustomObject(SessionStateKey, state);
        for (var targetIndex = 0; targetIndex < state.Targets.Count; targetIndex++)
        {
            var target = state.Targets[targetIndex];
            for (var track = 1; track <= 4; track++)
            {
                Cls();
                DrawWorldMap(track, state.Side);
                Println("TRAJECTORY HEADING");
                Println($"FLT {targetIndex + 1:00}-{track:00}  TRACK {track}/4");
                Println($"TARGET: {target}");
                Println($"DEFCON {state.Defcon}");
                await PauseAsync(token).ConfigureAwait(false);
            }
        }

        string[] phases =
        [
            "EARLY WARNING RADAR CONFIRMED",
            "IMPACT PROJECTION COMPLETE",
            "RETALIATORY STRIKE DETECTED",
            "SECOND STRIKE OPTIONS EXECUTED"
        ];
        for (var i = 0; i < phases.Length; i++)
        {
            Cls();
            DrawWorldMap(4, state.Side);
            state.Defcon = i < 2 ? 2 : 1;
            Println($"DEFCON {state.Defcon}");
            Println(phases[i]);
            Println($"PROJECTED LOSSES: {32 + (i * 17)} M");
            SetCustomObject(SessionStateKey, state);
            await PauseAsync(token).ConfigureAwait(false);
        }

        Cls();
        Println("GLOBAL THERMONUCLEAR WAR");
        Println("---------------------------------------");
        Println("US STRIKE:      LAUNCHED");
        Println("SOVIET STRIKE:  LAUNCHED");
        Println("SURVIVING POPULATION: UNKNOWN");
        Println("PROJECTED US LOSSES:   65 PERCENT");
        Println("PROJECTED USSR LOSSES: 72 PERCENT");
        Println("WINNER: NONE");
        Println();
        Print("PRESS ENTER FOR WOPR ANALYSIS: ");
        var input = await ReadAsync(8, token).ConfigureAwait(false);
        if (WoprInput.IsQuit(input)) return false;
        state.Scene = WoprScene.Learning;
        return true;
    }

    private async Task RunLearningSimulationAsync(WoprSessionState state, CancellationToken token)
    {
        for (var game = 1; game <= LearningGames; game++)
        {
            var board = new TicTacToeBoard();
            var mark = 'X';
            while (!board.IsFull && board.Winner() == '\0')
            {
                board.TryMove(board.BestMove(mark), mark);
                mark = mark == 'X' ? 'O' : 'X';
                Cls();
                Println("LEARNING...");
                Println($"SIMULATION {game:00}/{LearningGames}");
                PrintBoard(board);
                await PauseAsync(GetLearningDelay(game), token).ConfigureAwait(false);
            }

            Println("RESULT: DRAW");
            await PauseAsync(GetLearningDelay(game), token).ConfigureAwait(false);
        }

        await ShowStrategyAnalysisAsync(token).ConfigureAwait(false);

        state.Defcon = 5;
        Cls();
        Println("A STRANGE GAME.");
        Println();
        Println("THE ONLY WINNING MOVE IS NOT TO PLAY.");
        Println();
        Println("HOW ABOUT A NICE GAME OF CHESS?");
        Println();
        Println("DEFCON 5");
        Println();
        Print("PRESS ENTER TO RETURN TO BBS: ");
        await ReadAsync(1, token).ConfigureAwait(false);
        state.Scene = WoprScene.Complete;
    }

    private void DrawWorldMap(int track = 0, string side = "")
    {
        Println("      .--.       _       .---.");
        var marker = track switch { 1 => ".", 2 => "-", 3 => "=", 4 => ">", _ => " " };
        if (side == "SOVIET UNION") marker = track switch { 1 => ".", 2 => "-", 3 => "=", 4 => "<", _ => " " };
        Println($"  _.-' USA `{marker}._/ `{marker}.__.' USSR`-.");
        Println($" /      ATLANTIC {marker} | {marker} PACIFIC   /");
        Println(" `---._____.---.____|____.---.__.'");
        Println();
    }

    private async Task PrintSystemBootAsync(CancellationToken token)
    {
        string[] lines =
        [
            "#45  11456  11009  11893  11972",
            "PRT CON 3.4.5  SECTRAN 9.4.3",
            "PORT STAT: SD-345",
            "(311) 699-7305",
            "SYSPROC FUNCT READY",
            "ALT NET READY",
            "CPU AUTH RY-345-AX3",
            "SYSCOMP STATUS: ALL PORTS ACTIVE"
        ];
        Cls();
        foreach (var line in lines)
        {
            Println(line);
            await PauseAsync(TimeSpan.FromMilliseconds(Math.Max(20, AnimationDelay.TotalMilliseconds / 4)), token).ConfigureAwait(false);
        }
        Println();
    }

    private void PrintGamesList()
    {
        string[] games =
        [
            "FALKEN'S MAZE", "BLACK JACK", "GIN RUMMY",
            "HEARTS", "BRIDGE", "CHECKERS", "CHESS",
            "POKER", "FIGHTER COMBAT",
            "GUERRILLA ENGAGEMENT", "DESERT WARFARE",
            "AIR-TO-GROUND ACTIONS",
            "THEATERWIDE TACTICAL WARFARE",
            "BIOTOXIC AND CHEMICAL WARFARE",
            "GLOBAL THERMONUCLEAR WAR"
        ];
        Println();
        Println("AVAILABLE GAMES");
        Println("---------------------------------------");
        foreach (var game in games) Println(game);
        Println();
    }

    private async Task ShowStrategyAnalysisAsync(CancellationToken token)
    {
        string[] strategies =
        [
            "U.S. FIRST STRIKE", "USSR FIRST STRIKE",
            "NATO / WARSAW PACT", "FAR EAST STRATEGY",
            "US USSR ESCALATION", "MIDDLE EAST WAR",
            "INDIA PAKISTAN WAR", "CUBAN PROVOCATION",
            "ATLANTIC HEAVY", "PACIFIC TERRITORIAL",
            "ARCTIC MINIMAL", "NATO LIMITED",
            "TAIWAN THEATERWIDE", "IRANIAN MANEUVER",
            "AFRICAN TERRITORIAL", "EUROPEAN ALERT",
            "MEDITERRANEAN WAR", "PACIFIC DEFENSE",
            "NORTHERN MAXIMUM", "GLOBAL ESCALATION"
        ];
        const int pageSize = 5;
        var pages = (strategies.Length + pageSize - 1) / pageSize;
        for (var page = 0; page < pages; page++)
        {
            Cls();
            Println("STRATEGY ANALYSIS");
            Println($"PAGE {page + 1}/{pages}       WINNER");
            Println("---------------------------------------");
            foreach (var strategy in strategies.Skip(page * pageSize).Take(pageSize))
            {
                Println($"{strategy,-29} NONE");
            }
            Println();
            Println("ALL OUTCOMES: NO WINNER");
            await PauseAsync(token).ConfigureAwait(false);
        }
    }

    private void PrintBoard(TicTacToeBoard board)
    {
        for (var row = 0; row < 3; row++)
        {
            var a = Display(board[row * 3]);
            var b = Display(board[row * 3 + 1]);
            var c = Display(board[row * 3 + 2]);
            Println($" {a} | {b} | {c}");
            if (row < 2) Println("---+---+---");
        }
    }

    private static char Display(char value) => value == '\0' ? ' ' : value;

    private async Task<string> ReadAsync(int maxLength, CancellationToken token)
    {
        await FlushAsync(token).ConfigureAwait(false);
        return await ReadLineAsync(maxLength, token).ConfigureAwait(false);
    }

    private async Task PauseAsync(CancellationToken token)
    {
        await PauseAsync(AnimationDelay, token).ConfigureAwait(false);
    }

    private async Task PauseAsync(TimeSpan delay, CancellationToken token)
    {
        await FlushAsync(token).ConfigureAwait(false);
        await Task.Delay(delay, token).ConfigureAwait(false);
    }

    internal TimeSpan GetLearningDelay(int gameNumber)
    {
        var divisor = 1d + ((Math.Max(1, gameNumber) - 1) * 0.65d);
        return TimeSpan.FromMilliseconds(Math.Max(20d, AnimationDelay.TotalMilliseconds / divisor));
    }

    private WoprSessionState NewState()
    {
        var state = new WoprSessionState();
        SetCustomObject(SessionStateKey, state);
        return state;
    }
}

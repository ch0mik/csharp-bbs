using Bbs.Terminals;
using Bbs.Tenants.Content;
using Bbs.Tenants.Content.Quiz;

namespace Bbs.Tenants;

public sealed class QuizPetscii : PetsciiThread
{
    private const string SessionStateKey = "session:quiz:petscii:state";
    private const int QuestionsPerRound = 25;
    private const int QuestionWidth = 39;
    private const int QuestionLinesPerPage = 8;
    private const int PacksPageSize = 8;

    private readonly QuizPackLoader _packLoader = new();

    private sealed class QuizAnswerState
    {
        public string QuestionId { get; set; } = string.Empty;
        public string QuestionText { get; set; } = string.Empty;
        public string UserAnswer { get; set; } = string.Empty;
        public string CorrectAnswer { get; set; } = string.Empty;
        public bool IsCorrect { get; set; }
    }

    private sealed class QuizSessionState
    {
        public bool IsActive { get; set; }
        public string PackPath { get; set; } = string.Empty;
        public string PackId { get; set; } = string.Empty;
        public string PackLanguage { get; set; } = string.Empty;
        public string PackTitle { get; set; } = string.Empty;
        public string PackDescription { get; set; } = string.Empty;
        public int CurrentQuestionIndex { get; set; }
        public int CurrentPageIndex { get; set; }
        public int Score { get; set; }
        public List<int> QuestionOrder { get; set; } = new();
        public List<QuizAnswerState> Answers { get; set; } = new();
    }

    public override async Task DoLoopAsync(CancellationToken cancellationToken = default)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var packs = _packLoader.LoadAll();
            if (packs.Count == 0)
            {
                Cls();
                Println("Quiz packs not found.");
                Println();
                Println("Set QUIZ_PACKS_ROOT or mount:");
                Println(TextRender.TrimTo(_packLoader.PacksRoot, 39));
                Println();
                Print("Press ENTER to exit... ");
                await FlushAsync(cancellationToken).ConfigureAwait(false);
                await ReadLineAsync(maxLength: 1, cancellationToken: cancellationToken).ConfigureAwait(false);
                return;
            }

            var resumed = await TryResumeSessionAsync(packs, cancellationToken).ConfigureAwait(false);
            if (resumed == ResumeDecision.ReturnToMenu)
            {
                return;
            }

            if (resumed == ResumeDecision.Resumed)
            {
                continue;
            }

            var language = await SelectLanguageAsync(packs, cancellationToken).ConfigureAwait(false);
            if (language is null)
            {
                return;
            }

            var selectedPack = await SelectPackAsync(packs, language, cancellationToken).ConfigureAwait(false);
            if (selectedPack is null)
            {
                continue;
            }

            var confirmed = await ShowPackHeaderAndConfirmStartAsync(selectedPack, cancellationToken).ConfigureAwait(false);
            if (!confirmed)
            {
                continue;
            }

            var state = BuildNewState(selectedPack);
            SaveState(state);

            await PlayRoundAsync(state, selectedPack, cancellationToken).ConfigureAwait(false);
        }
    }

    private enum ResumeDecision
    {
        NoSession,
        Resumed,
        ReturnToMenu
    }

    private async Task<ResumeDecision> TryResumeSessionAsync(IReadOnlyList<QuizPack> packs, CancellationToken cancellationToken)
    {
        var state = GetState();
        if (state is null)
        {
            return ResumeDecision.NoSession;
        }

        var pack = packs.FirstOrDefault(x =>
            string.Equals(x.SourcePath, state.PackPath, StringComparison.OrdinalIgnoreCase)
            || string.Equals(x.Header.Id, state.PackId, StringComparison.OrdinalIgnoreCase));

        if (pack is null)
        {
            ClearState();
            return ResumeDecision.NoSession;
        }

        while (!cancellationToken.IsCancellationRequested)
        {
            Cls();
            Println("Quiz session found");
            Println(new string('-', 39));
            Println(TextRender.TrimTo(state.PackTitle, 39));
            Println($"Q: {state.CurrentQuestionIndex + 1}/{QuestionsPerRound}");
            Println();
            Println("R) Resume");
            Println("N) New game");
            Println(".) Back");
            Println();
            Print("Choice: ");
            await FlushAsync(cancellationToken).ConfigureAwait(false);

            var input = (await ReadLineAsync(maxLength: 6, cancellationToken: cancellationToken).ConfigureAwait(false)).Trim().ToUpperInvariant();
            if (input is "R" or "RESUME")
            {
                await PlayRoundAsync(state, pack, cancellationToken).ConfigureAwait(false);
                return ResumeDecision.Resumed;
            }

            if (input is "N" or "NEW")
            {
                ClearState();
                return ResumeDecision.NoSession;
            }

            if (input is "." or "Q" or "QUIT")
            {
                return ResumeDecision.ReturnToMenu;
            }
        }

        return ResumeDecision.ReturnToMenu;
    }

    private async Task<string?> SelectLanguageAsync(IReadOnlyList<QuizPack> packs, CancellationToken cancellationToken)
    {
        var languages = packs
            .Select(x => x.Header.Language.ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        while (!cancellationToken.IsCancellationRequested)
        {
            Cls();
            Println("Quiz PETSCII");
            Println(new string('-', 39));
            Println("Select language:");
            Println();
            for (var i = 0; i < languages.Length; i++)
            {
                var code = languages[i];
                var label = code == "pl" ? "Polski" : code == "en" ? "English" : code == "cz" ? "Cesky" : code;
                Println($"{i + 1}) {label} ({code})");
            }

            Println(".) Back");
            Println();
            Print("Choice: ");
            await FlushAsync(cancellationToken).ConfigureAwait(false);

            var input = (await ReadLineAsync(maxLength: 8, cancellationToken: cancellationToken).ConfigureAwait(false)).Trim().ToLowerInvariant();
            if (input is "." or "q" or "quit")
            {
                return null;
            }

            if (int.TryParse(input, out var idx) && idx >= 1 && idx <= languages.Length)
            {
                return languages[idx - 1];
            }

            var byCode = languages.FirstOrDefault(x => string.Equals(x, input, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(byCode))
            {
                return byCode;
            }
        }

        return null;
    }

    private async Task<QuizPack?> SelectPackAsync(IReadOnlyList<QuizPack> packs, string language, CancellationToken cancellationToken)
    {
        var languagePacks = packs
            .Where(x => string.Equals(x.Header.Language, language, StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x.Header.Title, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (languagePacks.Length == 0)
        {
            return null;
        }

        var page = 0;
        while (!cancellationToken.IsCancellationRequested)
        {
            var totalPages = Math.Max(1, (int)Math.Ceiling(languagePacks.Length / (double)PacksPageSize));
            page = Math.Clamp(page, 0, totalPages - 1);
            var pageItems = languagePacks.Skip(page * PacksPageSize).Take(PacksPageSize).ToArray();

            Cls();
            Println("Quiz packs");
            Println(new string('-', 39));
            Println($"Lang: {language.ToUpperInvariant()}  Page {page + 1}/{totalPages}");
            Println();

            for (var i = 0; i < pageItems.Length; i++)
            {
                var item = pageItems[i];
                Println($"{i + 1}) {TextRender.TrimTo(item.Header.Title, 30)}");
                Println($"   Q: {item.Questions.Count}  v{TextRender.TrimTo(item.Header.Version, 10)}");
            }

            Println();
            Print("1-8=open N+/N- . > ");
            await FlushAsync(cancellationToken).ConfigureAwait(false);

            var input = (await ReadLineAsync(maxLength: 8, cancellationToken: cancellationToken).ConfigureAwait(false)).Trim();
            if (input is "." or "q" or "Q")
            {
                return null;
            }

            if (string.Equals(input, "n+", StringComparison.OrdinalIgnoreCase)
                || string.Equals(input, "+", StringComparison.OrdinalIgnoreCase)
                || string.Equals(input, "n", StringComparison.OrdinalIgnoreCase))
            {
                if (page + 1 < totalPages)
                {
                    page++;
                }

                continue;
            }

            if (string.Equals(input, "n-", StringComparison.OrdinalIgnoreCase)
                || string.Equals(input, "-", StringComparison.OrdinalIgnoreCase)
                || string.Equals(input, "p", StringComparison.OrdinalIgnoreCase))
            {
                if (page > 0)
                {
                    page--;
                }

                continue;
            }

            if (!int.TryParse(input, out var idx) || idx < 1 || idx > pageItems.Length)
            {
                continue;
            }

            return pageItems[idx - 1];
        }

        return null;
    }

    private async Task<bool> ShowPackHeaderAndConfirmStartAsync(QuizPack pack, CancellationToken cancellationToken)
    {
        var wrapped = TextRender.WrapLines(pack.Header.Description, 39);
        var page = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            Cls();
            Println(TextRender.TrimTo(pack.Header.Title, 39));
            Println(new string('-', 39));
            Println($"ID: {TextRender.TrimTo(pack.Header.Id, 35)}");
            Println($"Lang: {pack.Header.Language.ToUpperInvariant()}");
            Println($"Questions: {pack.Questions.Count}");
            Println($"Version: {TextRender.TrimTo(pack.Header.Version, 30)}");
            if (!string.IsNullOrWhiteSpace(pack.Header.Author))
            {
                Println($"Author: {TextRender.TrimTo(pack.Header.Author, 31)}");
            }

            Println();
            foreach (var row in wrapped.Skip(page * 8).Take(8))
            {
                Println(row);
            }

            var totalPages = Math.Max(1, (int)Math.Ceiling(wrapped.Count / 8.0));
            Println();
            Println($"Desc page {page + 1}/{totalPages}");
            Print("S=start N/P desc .=back > ");
            await FlushAsync(cancellationToken).ConfigureAwait(false);

            var input = (await ReadLineAsync(maxLength: 8, cancellationToken: cancellationToken).ConfigureAwait(false)).Trim().ToUpperInvariant();
            if (input is "S" or "START")
            {
                return true;
            }

            if (input is "." or "Q" or "QUIT")
            {
                return false;
            }

            if (input is "N" or "N+")
            {
                if (page + 1 < totalPages)
                {
                    page++;
                }

                continue;
            }

            if ((input is "P" or "N-") && page > 0)
            {
                page--;
            }
        }

        return false;
    }

    private QuizSessionState BuildNewState(QuizPack pack)
    {
        var order = Enumerable.Range(0, pack.Questions.Count)
            .OrderBy(_ => Random.Shared.Next())
            .Take(QuestionsPerRound)
            .ToList();

        return new QuizSessionState
        {
            IsActive = true,
            PackPath = pack.SourcePath,
            PackId = pack.Header.Id,
            PackLanguage = pack.Header.Language,
            PackTitle = pack.Header.Title,
            PackDescription = pack.Header.Description,
            CurrentQuestionIndex = 0,
            CurrentPageIndex = 0,
            Score = 0,
            QuestionOrder = order,
            Answers = new List<QuizAnswerState>(QuestionsPerRound)
        };
    }

    private async Task PlayRoundAsync(QuizSessionState state, QuizPack pack, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && state.CurrentQuestionIndex < QuestionsPerRound)
        {
            if (state.CurrentQuestionIndex >= state.QuestionOrder.Count)
            {
                break;
            }

            var qIndex = state.QuestionOrder[state.CurrentQuestionIndex];
            if (qIndex < 0 || qIndex >= pack.Questions.Count)
            {
                state.CurrentQuestionIndex++;
                state.CurrentPageIndex = 0;
                SaveState(state);
                continue;
            }

            var question = pack.Questions[qIndex];
            var pages = BuildQuestionPages(question.Q);
            if (pages.Count == 0)
            {
                pages = new List<IReadOnlyList<string>> { Array.Empty<string>() };
            }

            state.CurrentPageIndex = Math.Clamp(state.CurrentPageIndex, 0, pages.Count - 1);
            RenderQuestionScreen(state, question, pages);

            Print("A/B/C/D N/P . > ");
            await FlushAsync(cancellationToken).ConfigureAwait(false);

            var input = (await ReadLineAsync(maxLength: 4, cancellationToken: cancellationToken).ConfigureAwait(false)).Trim().ToUpperInvariant();
            if (input is "N" or "N+")
            {
                if (state.CurrentPageIndex + 1 < pages.Count)
                {
                    state.CurrentPageIndex++;
                    SaveState(state);
                }

                continue;
            }

            if (input is "P" or "N-")
            {
                if (state.CurrentPageIndex > 0)
                {
                    state.CurrentPageIndex--;
                    SaveState(state);
                }

                continue;
            }

            if (input == ".")
            {
                var exit = await ConfirmExitAsync(cancellationToken).ConfigureAwait(false);
                if (exit)
                {
                    SaveState(state);
                    return;
                }

                continue;
            }

            if (input is not "A" and not "B" and not "C" and not "D")
            {
                continue;
            }

            var correct = string.Equals(question.Correct, input, StringComparison.OrdinalIgnoreCase);
            if (correct)
            {
                state.Score++;
            }

            state.Answers.Add(new QuizAnswerState
            {
                QuestionId = question.Id,
                QuestionText = question.Q,
                UserAnswer = input,
                CorrectAnswer = question.Correct,
                IsCorrect = correct
            });

            state.CurrentQuestionIndex++;
            state.CurrentPageIndex = 0;
            SaveState(state);
        }

        await ShowSummaryAsync(state, cancellationToken).ConfigureAwait(false);
        ClearState();
    }

    private void RenderQuestionScreen(QuizSessionState state, QuizQuestion question, IReadOnlyList<IReadOnlyList<string>> pages)
    {
        var page = pages[state.CurrentPageIndex];

        Cls();
        Write(PetsciiKeys.LightBlue);
        Println(TextRender.TrimTo(state.PackTitle, 39));
        Write(PetsciiKeys.White);
        Println($"Q {state.CurrentQuestionIndex + 1}/{QuestionsPerRound}");
        Println($"Page {state.CurrentPageIndex + 1}/{pages.Count}");
        Println();

        foreach (var row in page)
        {
            Println(row);
        }

        var fill = QuestionLinesPerPage - page.Count;
        for (var i = 0; i < fill; i++)
        {
            Println();
        }

        Println();
        Println($"A) {TextRender.TrimTo(question.A, 35)}");
        Println($"B) {TextRender.TrimTo(question.B, 35)}");
        Println($"C) {TextRender.TrimTo(question.C, 35)}");
        Println($"D) {TextRender.TrimTo(question.D, 35)}");
        Println();
    }

    private List<IReadOnlyList<string>> BuildQuestionPages(string question)
    {
        var lines = TextRender.WrapLines(question, QuestionWidth).ToList();
        if (lines.Count == 0)
        {
            lines.Add(string.Empty);
        }

        var pages = new List<IReadOnlyList<string>>();
        for (var i = 0; i < lines.Count; i += QuestionLinesPerPage)
        {
            pages.Add(lines.Skip(i).Take(QuestionLinesPerPage).ToArray());
        }

        return pages;
    }

    private async Task<bool> ConfirmExitAsync(CancellationToken cancellationToken)
    {
        Println();
        Print("Exit game? Y/N > ");
        await FlushAsync(cancellationToken).ConfigureAwait(false);

        var input = (await ReadLineAsync(maxLength: 2, cancellationToken: cancellationToken).ConfigureAwait(false)).Trim().ToUpperInvariant();
        return input is "Y" or "T" or "YES" or "TAK";
    }

    private async Task ShowSummaryAsync(QuizSessionState state, CancellationToken cancellationToken)
    {
        var total = QuestionsPerRound;
        var pct = total <= 0 ? 0 : (int)Math.Round((state.Score * 100.0) / total, MidpointRounding.AwayFromZero);
        var (color, label, faceLines) = GetFaceByPercent(pct, state.PackLanguage);

        Cls();
        Write(PetsciiKeys.LightBlue);
        Println("Round complete!");
        Write(PetsciiKeys.White);
        Println(new string('-', 39));
        Println($"Score: {state.Score}/{total}");
        Println($"Percent: {pct}%");
        Println();

        Write(color);
        Println(TextRender.TrimTo(label, 39));
        foreach (var line in faceLines)
        {
            Println(TextRender.TrimTo(line, 39));
        }

        Write(PetsciiKeys.White);
        Println();
        Print("Press ENTER... ");
        await FlushAsync(cancellationToken).ConfigureAwait(false);
        await ReadLineAsync(maxLength: 1, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private static (int Color, string Label, string[] FaceLines) GetFaceByPercent(int pct, string? language)
    {
        var lang = (language ?? string.Empty).Trim().ToLowerInvariant();

        if (pct <= 30)
        {
            return (
                PetsciiKeys.Red,
                ResolveLabel(lang, "Dawaj dalej!", "Zkus to znovu!", "Keep trying!"),
                new[]
                {
                    "      .--------.",
                    "      |  o  o  |",
                    "      |   ..   |",
                    "      |  /  \\  |",
                    "      '--------'"
                });
        }

        if (pct <= 50)
        {
            return (
                PetsciiKeys.Orange,
                ResolveLabel(lang, "Niezle.", "Neni to spatne.", "Not bad."),
                new[]
                {
                    "      .--------.",
                    "      |  o  o  |",
                    "      |   ..   |",
                    "      |  /--   |",
                    "      '--------'"
                });
        }

        if (pct <= 70)
        {
            return (
                PetsciiKeys.Yellow,
                ResolveLabel(lang, "Solidny wynik.", "Solidni vysledek.", "Solid run."),
                new[]
                {
                    "      .--------.",
                    "      |  o  o  |",
                    "      |   ..   |",
                    "      |  ----  |",
                    "      '--------'"
                });
        }

        if (pct <= 90)
        {
            return (
                PetsciiKeys.LightGreen,
                ResolveLabel(lang, "Swietny wynik!", "Skorely vysledek!", "Great score!"),
                new[]
                {
                    "      .--------.",
                    "      |  o  o  |",
                    "      |   ..   |",
                    "      |  \\__/  |",
                    "      '--------'"
                });
        }

        return (
            PetsciiKeys.Cyan,
            ResolveLabel(lang, "Mistrzowski wynik!", "Perfektni vysledek!", "Perfect vibe!"),
            new[]
            {
                "      .--------.",
                "      |  O  O  |",
                "      |   ..   |",
                "      | \\____/ |",
                "      '--------'"
            });
    }

    private static string ResolveLabel(string lang, string pl, string cz, string en)
    {
        return lang switch
        {
            "pl" => pl,
            "cz" => cz,
            _ => en
        };
    }

    private QuizSessionState? GetState()
    {
        var value = GetCustomObject(SessionStateKey);
        if (value is not QuizSessionState state || !state.IsActive)
        {
            return null;
        }

        return state;
    }

    private void SaveState(QuizSessionState state)
    {
        state.IsActive = true;
        SetCustomObject(SessionStateKey, state);
    }

    private void ClearState()
    {
        SetCustomObject(SessionStateKey, new QuizSessionState { IsActive = false });
    }
}

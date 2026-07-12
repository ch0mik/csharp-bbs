using Bbs.Terminals;
using Bbs.Tenants.Content.School;

namespace Bbs.Tenants;

public sealed class SchoolPetscii : PetsciiThread
{
    private const string SessionStateKey = "session:school:state";
    private const int RecordsPerPage = 3;

    public override async Task DoLoopAsync(CancellationToken cancellationToken = default)
    {
        if (!await AuthenticateAsync(cancellationToken).ConfigureAwait(false)) return;
        var state = GetCustomObject(SessionStateKey) as SchoolSessionState ?? new SchoolSessionState();
        SetCustomObject(SessionStateKey, state);

        while (!cancellationToken.IsCancellationRequested)
        {
            Cls();
            Println("SEATTLE PUBLIC SCHOOL DISTRICT");
            Println("STUDENT RECORDS TERMINAL");
            Println("---------------------------------------");
            Println("ENTER A STUDENT NAME OR SURNAME.");
            Println("EXAMPLES: LIGHTMAN, MACK");
            Println(".) DISCONNECT");
            Println();
            Print("STUDENT NAME: ");
            var query = await ReadAsync(30, cancellationToken).ConfigureAwait(false);
            if (SchoolInput.IsQuit(query)) return;

            var records = state.Search(query);
            if (records.Count == 0)
            {
                Println("NO STUDENT RECORD FOUND");
                await WaitForEnterAsync(cancellationToken).ConfigureAwait(false);
                continue;
            }

            if (!await BrowseStudentAsync(state, records, cancellationToken).ConfigureAwait(false)) return;
        }
    }

    private async Task<bool> AuthenticateAsync(CancellationToken token)
    {
        Cls();
        Println("PDP 11/270 PRB TIP #45");
        Println("TTY 34/984");
        Println();
        Println("WELCOME TO THE SEATTLE PUBLIC");
        Println("SCHOOL DISTRICT DATANET");
        Println();

        for (var attempt = 1; attempt <= 3; attempt++)
        {
            Print("USER PASSWORD: ");
            var password = await ReadAsync(20, token).ConfigureAwait(false);
            if (SchoolInput.IsQuit(password)) return false;
            if (SchoolInput.IsPasswordValid(password))
            {
                Println("PASSWORD VERIFIED");
                return true;
            }

            Println("INVALID PASSWORD");
            Println($"ATTEMPT {attempt}/3");
        }

        Println("TOO MANY INVALID PASSWORD ATTEMPTS");
        Println("--DISCONNECTED--");
        await FlushAsync(token).ConfigureAwait(false);
        return false;
    }

    private async Task<bool> BrowseStudentAsync(SchoolSessionState state, IReadOnlyList<SchoolCourseRecord> records, CancellationToken token)
    {
        var page = 0;
        var pageCount = (records.Count + RecordsPerPage - 1) / RecordsPerPage;
        while (!token.IsCancellationRequested)
        {
            page = Math.Clamp(page, 0, pageCount - 1);
            Cls();
            Println($"STUDENT: {records[0].StudentName}");
            Println($"RECORDS PAGE {page + 1}/{pageCount}");
            Println("---------------------------------------");
            foreach (var record in records.Skip(page * RecordsPerPage).Take(RecordsPerPage))
            {
                Println($"{record.ClassNumber,-6} {Trim(record.CourseTitle, 18),-18} {record.Grade}");
                Println($"  {Trim(record.Teacher, 14),-14} P:{record.Period} R:{record.Room}");
            }

            Println("---------------------------------------");
            Println("N) NEXT  P) PREV  E) EDIT");
            Println("S) SEARCH  .) DISCONNECT");
            Print("OPTION: ");
            var option = (await ReadAsync(10, token).ConfigureAwait(false)).Trim().ToUpperInvariant();
            if (SchoolInput.IsQuit(option)) return false;
            if (option is "S" or "SEARCH") return true;
            if (option is "N" or "NEXT") { page = (page + 1) % pageCount; continue; }
            if (option is "P" or "PREV") { page = (page - 1 + pageCount) % pageCount; continue; }
            if (option is "E" or "EDIT")
            {
                await EditGradeAsync(state, records, token).ConfigureAwait(false);
                continue;
            }
            Println("INVALID OPTION");
        }

        return false;
    }

    private async Task EditGradeAsync(SchoolSessionState state, IReadOnlyList<SchoolCourseRecord> records, CancellationToken token)
    {
        Print("CLASS TO CHANGE: ");
        var classNumber = await ReadAsync(10, token).ConfigureAwait(false);
        if (!records.Any(r => string.Equals(r.ClassNumber, classNumber.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            Println("NO MATCH ON CLASS FOR STUDENT");
            await WaitForEnterAsync(token).ConfigureAwait(false);
            return;
        }

        Print("NEW GRADE (A-F,U): ");
        var grade = await ReadAsync(1, token).ConfigureAwait(false);
        if (!state.TryChangeGrade(records, classNumber, grade))
        {
            Println("INVALID GRADE - RECORD LOCKED");
            await WaitForEnterAsync(token).ConfigureAwait(false);
            return;
        }

        SetCustomObject(SessionStateKey, state);
        Println("STUDENT RECORD UPDATED");
        await WaitForEnterAsync(token).ConfigureAwait(false);
    }

    private async Task<string> ReadAsync(int maxLength, CancellationToken token)
    {
        await FlushAsync(token).ConfigureAwait(false);
        return await ReadLineAsync(maxLength, token).ConfigureAwait(false);
    }

    private async Task WaitForEnterAsync(CancellationToken token)
    {
        Print("PRESS ENTER: ");
        await ReadAsync(1, token).ConfigureAwait(false);
    }

    private static string Trim(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..maxLength];
}

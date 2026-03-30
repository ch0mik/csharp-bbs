using Bbs.Core.Content;
using Bbs.Core.Resources;
using Bbs.Terminals;
using Bbs.Tenants.Content;

namespace Bbs.Tenants;

public sealed class ZorkMachine : PetsciiThread
{
    private readonly IZMachineService _zmachine = new ZMachineService();
    private readonly IResourceProvider _resources = new ResourceProvider(typeof(ZorkMachine).Assembly);

    public override async Task DoLoopAsync(CancellationToken cancellationToken = default)
    {
        var stories = GetStoryCandidates();

        while (!cancellationToken.IsCancellationRequested)
        {
            Cls();
            Println("ZorkMachine");
            Println(new string('-', 39));
            for (var i = 0; i < stories.Length; i++)
            {
                Println($"{i + 1}) {stories[i].Name}");
            }
            Println(".) Back");
            Print("Choice: ");
            await FlushAsync(cancellationToken).ConfigureAwait(false);

            var input = (await ReadLineAsync(maxLength: 4, cancellationToken: cancellationToken).ConfigureAwait(false)).Trim();
            if (input == ".")
            {
                return;
            }

            if (!int.TryParse(input, out var idx) || idx < 1 || idx > stories.Length)
            {
                continue;
            }

            var selected = stories[idx - 1];
            byte[] story;
            if (!_resources.TryReadBinary(selected.Path, out story))
            {
                if (!File.Exists(selected.Path))
                {
                    Cls();
                    Println("Story file not found:");
                    Println(TextRender.TrimTo(selected.Path, 39));
                    Print("Press ENTER...");
                    await FlushAsync(cancellationToken).ConfigureAwait(false);
                    await ReadLineAsync(maxLength: 1, cancellationToken: cancellationToken).ConfigureAwait(false);
                    continue;
                }

                story = await File.ReadAllBytesAsync(selected.Path, cancellationToken).ConfigureAwait(false);
            }

            await _zmachine.RunAsync(
                selected.Name,
                story,
                async output =>
                {
                    Print(output);
                    await FlushAsync(cancellationToken).ConfigureAwait(false);
                },
                async () => await ReadLineAsync(maxLength: 128, cancellationToken: cancellationToken).ConfigureAwait(false),
                cancellationToken).ConfigureAwait(false);

            Println();
            Print("Press ENTER...");
            await FlushAsync(cancellationToken).ConfigureAwait(false);
            await ReadLineAsync(maxLength: 1, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
    }

    private static (string Name, string Path)[] GetStoryCandidates()
    {
        var root = Environment.GetEnvironmentVariable("ZMACHINE_STORY_ROOT");
        if (string.IsNullOrWhiteSpace(root))
        {
            root = Path.Combine(AppContext.BaseDirectory, "zmpp");
        }
        else
        {
            root = root.Trim();
        }

        return
        [
            ("zork1", Path.Combine(root, "zork1.z3")),
            ("zork2", Path.Combine(root, "zork2.z3")),
            ("zork3", Path.Combine(root, "zork3.z3")),
            ("hitchhiker", Path.Combine(root, "hitchhiker-r60.z3")),
            ("planetfall", Path.Combine(root, "planetfall-r39.z3"))
        ];
    }
}

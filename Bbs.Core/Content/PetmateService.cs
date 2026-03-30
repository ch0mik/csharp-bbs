using System.Net;
using System.Text.Json;

namespace Bbs.Core.Content;

/// <summary>
/// Renders Petmate JSON format (.petmate files) to PETSCII byte sequences.
/// Based on Java implementation by CityXen.
/// </summary>
public class PetmateService
{
    // Character code mapping for Petmate JSON (6 ranges)
    private static Dictionary<int, int> BuildCharacterCodeMap()
    {
        var map = new Dictionary<int, int>();
        // Range 1: 0-31 -> +64
        for (int i = 0; i < 32; i++) map[i] = i + 64;
        // Range 2: 64-95 -> +128
        for (int i = 64; i < 96; i++) map[i] = i + 128;
        // Range 3: 96-127 -> +64
        for (int i = 96; i < 128; i++) map[i] = i + 64;
        // Range 4: 128-159 -> -64
        for (int i = 128; i < 160; i++) map[i] = i - 64;
        // Range 5: 160-191 -> -128
        for (int i = 160; i < 192; i++) map[i] = i - 128;
        // Range 6: 224-255 -> -64
        for (int i = 224; i < 256; i++) map[i] = i - 64;
        return map;
    }

    private static readonly Dictionary<int, int> CharacterCodeMap = BuildCharacterCodeMap();

    // PETSCII color codes array (16 colors)
    private static readonly int[] ColorCodes =
    [
        0,  // BLACK
        1,  // WHITE
        2,  // RED
        3,  // CYAN
        4,  // PURPLE
        5,  // GREEN
        6,  // BLUE
        7,  // YELLOW
        8,  // ORANGE
        9,  // BROWN
        10, // LIGHT_RED
        11, // GREY1
        12, // GREY2
        13, // LIGHT_GREEN
        14, // LIGHT_BLUE
        15  // GREY3
    ];

    /// <summary>
    /// Parses and renders Petmate JSON format to PETSCII byte array.
    /// </summary>
    public static byte[] RenderPetmateJson(
        string jsonContent,
        int xOffset = 0,
        int yOffset = 0)
    {
        try
        {
            using (var doc = JsonDocument.Parse(jsonContent))
            {
                var root = doc.RootElement;
                var framebufs = root.GetProperty("framebufs");
                var framebuf = framebufs[0];

                var screencodesArray = framebuf.GetProperty("screencodes");
                var colorsArray = framebuf.GetProperty("colors");
                var charset = framebuf.GetProperty("charset").GetString() ?? "upper";
                var width = framebuf.GetProperty("width").GetInt64();
                var height = framebuf.GetProperty("height").GetInt64();

                var output = new List<int>();

                // Charset mode (9=UPPERCASE, 14=LOWERCASE, 8=CASE_LOCK)
                output.Add("upper".Equals(charset, StringComparison.OrdinalIgnoreCase) ? 142 : 14);
                output.Add(8); // CASE_LOCK
                output.Add(19); // HOME

                // Positioning
                if (yOffset > 0)
                {
                    for (int i = 0; i < yOffset; i++) output.Add(17); // DOWN
                }
                if (xOffset > 0)
                {
                    for (int i = 0; i < xOffset; i++) output.Add(29); // RIGHT
                }

                int lineCounter = 0;
                int? lastColor = null;

                for (int i = 0; i < screencodesArray.GetArrayLength(); i++)
                {
                    // Line wrapping for width < 40
                    if (width < 40 && lineCounter >= width)
                    {
                        output.Add(17); // DOWN
                        for (int j = 0; j < width; j++) output.Add(157); // LEFT
                        lineCounter = 0;
                    }

                    // Get screencode and color
                    int screencode = screencodesArray[i].GetInt32();
                    int colorIndex = colorsArray[i].GetInt32();

                    // Map screencode
                    if (CharacterCodeMap.TryGetValue(screencode, out var mappedCode))
                    {
                        screencode = mappedCode;
                    }

                    // Add color if changed
                    int outColor = ColorCodes[colorIndex % ColorCodes.Length];
                    if (!lastColor.HasValue || lastColor != outColor)
                    {
                        output.Add(outColor);
                        lastColor = outColor;
                    }

                    // Add screencode
                    output.Add(screencode);
                    lineCounter++;
                }

                return output.Select(x => (byte)x).ToArray();
            }
        }
        catch (Exception ex)
        {
            // Fallback: return error message as bytes
            return System.Text.Encoding.ASCII.GetBytes($"ERROR: {ex.Message}");
        }
    }

    /// <summary>
    /// Checks if a file is Petmate JSON format.
    /// </summary>
    public static bool IsPetmateJson(string filename)
    {
        return filename.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ||
               filename.EndsWith(".petmate", StringComparison.OrdinalIgnoreCase);
    }
}

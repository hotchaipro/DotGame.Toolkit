// Copyright (c) 2026 Hot Chai Games LLC. Licensed under the MIT License.

using DotGame.Toolkit.ContentPipeline.BuildTool.Audio;
using DotGame.Toolkit.ContentPipeline.BuildTool.Fonts;
using DotGame.Toolkit.ContentPipeline.BuildTool.Graphics;

namespace DotGame.Toolkit.ContentPipeline.BuildTool;

internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
        {
            Console.Out.Write(Usage);
            return args.Length == 0 ? 1 : 0;
        }

        string command = args[0];
        string errorCode = command switch
        {
            TextureAtlasCommand.Name => TextureAtlasCommand.ErrorCode,
            SoundBankCommand.Name => SoundBankCommand.ErrorCode,
            SpriteFontCommand.Name => SpriteFontCommand.ErrorCode,
            _ => "CPB0001",
        };

        ContentBuildOutput.Origin = "DotGame.Toolkit.ContentPipeline.BuildTool";
        return ContentBuildOutput.Run(errorCode, () =>
        {
            var options = CommandLineOptions.Parse(args.AsSpan(1));
            switch (command)
            {
                case TextureAtlasCommand.Name:
                    TextureAtlasCommand.Run(options);
                    break;
                case SoundBankCommand.Name:
                    SoundBankCommand.Run(options);
                    break;
                case SpriteFontCommand.Name:
                    SpriteFontCommand.Run(options);
                    break;
                default:
                    throw new ContentBuildException($"Unknown command '{command}'. Run without arguments for usage.");
            }
        });
    }

    private const string Usage =
        """
        DotGame.Toolkit.ContentPipeline.BuildTool - builds the DotGame toolkit content types.
        Normally invoked by ContentPipeline.Toolkit.targets.

        Usage: DotGame.Toolkit.ContentPipeline.BuildTool <command> [--option value]...

        Commands:

          atlas       Packs images into texture atlas pages and writes the atlas descriptor (JSON).
                        --input-list <file>                 Image paths, separated by ';' or line breaks
                        --output <file>                     Descriptor (.json)
                        --texture-output-base <path>        Page path without the _<n><ext> suffix
                        --texture-output-extension <ext>    Final page extension, e.g. .webp
                        --encode-command-file <file>        Texture encode command template
                        --intermediate-dir <dir>
                        [--padding <px>] [--extrude <px>] [--max-width <px>] [--max-height <px>]
                        [--power-of-two] [--square]
                        [--ffmpeg <exe>]                    Decodes images other than 8-bit PNGs
                        [--input-display-dir <dir>] [--output-display-file <file>] [--atlas-display-name <name>]

          soundbank   Concatenates sounds (WAV, FLAC or Ogg Opus) into one audio file and writes the bank descriptor (JSON).
                        --input-list <file>                 Audio paths, separated by ';' or line breaks
                        --output <file>                     Descriptor (.json)
                        --audio-output <file>               Encoded audio file
                        --encode-command-file <file>        Sound encode command template
                        --intermediate-dir <dir>
                        [--sample-rate <hz>] [--channels <1|2>]
                        [--input-display-dir <dir>] [--output-display-file <file>]
                        [--audio-display-file <file>] [--bank-display-name <name>]

          spritefont  Renders a font with glyphgen and writes the .spritefont descriptor and atlas pages.
                        --settings <file>                   Settings sidecar written by the targets
                        --glyphgen <exe>
                        --output-base <path>                Output path without extension
                        --texture-output-extension <ext>
                        --intermediate-dir <dir>
                        [--display-name <name>]

        Encode command templates are run with cmd (Windows) or sh (macOS, Linux) after the
        {InputFile}, {OutputFile} and {IntermediateDir} tokens are substituted.

        """;
}

using System.IO.Compression;
using C64Bot.Libretro;
using C64Bot.Libretro.Tests;

namespace C64Bot.LodeRunner.Tests;

[Collection(LodeRunnerCollection.Name)]
public class LevelPictureTests(LodeRunnerReadyToStart lodeRunner)
{
    // Where the level is in the game's picture, and in LevelPicture's: its numbers take room at the top and the left.
    private const int GameLeft = 52;
    private const int GameTop = 35;
    private const int PictureLeft = 14;
    private const int PictureTop = 8;

    [Fact]
    public void Png_TwiceAsBig_IsTwiceAsWideAndHigh()
    {
        // Act
        var (width, height, _) = Decode(LevelPicture.Png("P", scale: 2));

        // Assert
        Assert.Equal((2 * (PictureLeft + 280), 2 * (PictureTop + 176)), (width, height));
    }

    [CoreFact]
    public void Png_MadeUpLevel_IsWhatTheGameShows()
    {
        // Arrange
        var level = Level.Parse(1, """
             #@H-$  $$ ---  HHH  @@##
            ##########@@@@@@H#########
                            H
            @P              H     $
            ############################
            """);
        lodeRunner.Starter.StartLevel(level);
        lodeRunner.RunUntilSetUp();
        int[]? game = null;
        var gamePitch = 0;
        lodeRunner.Core.VideoFrame = (pixels, width, height, pitch) =>
        {
            game = new int[pixels.Length / 4];
            for (var i = 0; i < game.Length; i++)
                game[i] = pixels[4 * i + 2] << 16 | pixels[4 * i + 1] << 8 | pixels[4 * i];
            gamePitch = pitch / 4;
        };
        lodeRunner.Core.RunFrame();
        lodeRunner.Core.VideoFrame = null;

        // Act
        var (pictureWidth, _, picture) = Decode(LevelPicture.Png(level.ToString(), scale: 1));

        // Assert, but for the player's tile: the game shows him only once he has moved.
        var differences = 0;
        for (var y = 0; y < Level.Height * 11; y++)
        {
            for (var x = 0; x < Level.Width * 10; x++)
            {
                if ((x / 10, y / 11) == (1, 3))
                    continue;

                if (picture[(PictureTop + y) * pictureWidth + PictureLeft + x] != game![(GameTop + y) * gamePitch + GameLeft + x])
                    differences++;
            }
        }

        Assert.Equal(0, differences);
    }

    // The width, height and colours of a PNG as LevelPicture writes them: one chunk of rows, without filters.
    private static (int Width, int Height, int[] Pixels) Decode(byte[] png)
    {
        int BigEndian(int at) => png[at] << 24 | png[at + 1] << 16 | png[at + 2] << 8 | png[at + 3];
        var (width, height) = (BigEndian(16), BigEndian(20));
        var at = 33;
        while (System.Text.Encoding.ASCII.GetString(png, at + 4, 4) != "IDAT")
            at += 12 + BigEndian(at);

        var rows = new MemoryStream();
        using (var zlib = new ZLibStream(new MemoryStream(png, at + 8, BigEndian(at)), CompressionMode.Decompress))
            zlib.CopyTo(rows);

        var bytes = rows.ToArray();
        var pixels = new int[width * height];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var i = y * (1 + 3 * width) + 1 + 3 * x;
                pixels[y * width + x] = bytes[i] << 16 | bytes[i + 1] << 8 | bytes[i + 2];
            }
        }

        return (width, height, pixels);
    }
}

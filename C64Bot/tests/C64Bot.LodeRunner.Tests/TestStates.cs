namespace C64Bot.LodeRunner.Tests;

// Game states written as text, for tests of the rules and of what's built on them.
internal static class TestStates
{
    // A state from text as Level.Parse reads it, with o for a hole. The player stands at the P, or in that row at the
    // column given, and on the tile given. Read from RAM laid out as the game has it. There's plenty of gold left,
    // unless something else is given, so that he isn't done just by standing on the top row.
    public static GameState State(
        string text,
        int? column = null,
        int offsetX = 0,
        int offsetY = 0,
        Tile on = Tile.Empty,
        byte pose = 0x08,
        bool falling = false,
        Direction digSide = Direction.Right,
        Direction? digging = null,
        int digPasses = 0,
        bool standsInHole = false,
        int holePasses = 100,
        int goldLeft = 9)
    {
        var ram = new byte[0x10000];
        var holes = new List<TilePosition>();
        var player = default(TilePosition);
        var hiddenLadders = 0;
        var lines = text.ReplaceLineEndings("\n").Split('\n');
        for (var row = 0; row < lines.Length; row++)
            for (var c = 0; c < lines[row].Length; c++)
                ram[RamMap.TileAddress(RamMap.LevelAsBuilt, c, row)] = (byte)ReadTile(lines[row][c], new(c, row));

        if (column is { } given)
            player = player with { Column = given };
        ram[RamMap.TileAddress(RamMap.LevelAsBuilt, player.Column, player.Row)] = (byte)on;
        if (standsInHole)
            holes.Add(player);

        (ram[RamMap.PlayerColumn], ram[RamMap.PlayerRow]) = ((byte)player.Column, (byte)player.Row);
        (ram[RamMap.PlayerStepX], ram[RamMap.PlayerStepY]) = ((byte)(offsetX + 2), (byte)(offsetY + 2));
        ram[RamMap.PlayerSprite] = pose;
        ram[RamMap.PlayerAlive] = 1;
        ram[RamMap.PlayerFalling] = (byte)(falling ? 0x00 : 0x20);
        ram[RamMap.PlayerLastSide] = Side(digSide);
        ram[RamMap.PlayerDigging] = digging is { } side ? Side(side) : (byte)0;
        ram[RamMap.PlayerDigStep] = (byte)(digPasses + (digging == Direction.Right ? RamMap.DigStepsRight : 0));
        for (var slot = 0; slot < holes.Count; slot++)
        {
            (ram[RamMap.HoleColumns + slot], ram[RamMap.HoleRows + slot]) =
                ((byte)holes[slot].Column, (byte)holes[slot].Row);
            ram[RamMap.HoleTimers + slot] = (byte)holePasses;
        }

        ram[RamMap.GoldLeft] = (byte)goldLeft;
        (ram[RamMap.MainCodePosition], ram[RamMap.MainCodePosition + 1]) = (0x55, 0x61);
        Assert.True(GameState.TryRead(ram, out var state));
        return state;

        Tile ReadTile(char character, TilePosition position)
        {
            switch (character)
            {
                case 'P':
                    player = position;
                    return Tile.Empty;
                case 'o':
                    holes.Add(position);
                    return Tile.Brick;
                case 'h':
                    hiddenLadders = ++ram[RamMap.HiddenLadderCount];
                    (ram[RamMap.HiddenLadderColumns + hiddenLadders], ram[RamMap.HiddenLadderRows + hiddenLadders]) =
                        ((byte)position.Column, (byte)position.Row);
                    return Tile.Empty;
                case 'G':
                    var guard = ++ram[RamMap.GuardCount];
                    (ram[RamMap.GuardColumns + guard], ram[RamMap.GuardRows + guard]) =
                        ((byte)position.Column, (byte)position.Row);
                    (ram[RamMap.GuardStepsX + guard], ram[RamMap.GuardStepsY + guard]) = (2, 2);
                    return Tile.Empty;
                default:
                    return Level.Parse(1, character.ToString())[0, 0];
            }
        }

        static byte Side(Direction side) => (byte)(side == Direction.Left ? 0xFF : 0x01);
    }
}

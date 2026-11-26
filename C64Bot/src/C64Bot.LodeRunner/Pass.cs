using C64Bot.Libretro;

namespace C64Bot.LodeRunner;

// Works out one pass of the game's main loop, by the game's own rules (from the disassembly): a working copy of a state
// is changed in the order the game does it. Used by GameState.Next, which describes it.
internal sealed class Pass
{
    // A figure is at most this far from the middle of his tile; one step more and he's on the next tile.
    private const int MaxOffset = 2;

    private const int PassesBeforeHole = 12;
    private const int PassesInDigPose = 6;
    private const int ScoreForGold = 250;

    private readonly GameState _before;
    private readonly List<Hole> _holes;
    private Level _level;
    private int _column;
    private int _row;
    private int _offsetX;
    private int _offsetY;
    private Pose _pose;
    private Direction? _facing;
    private bool _falling;
    private Direction? _digging;
    private int _digPasses;
    private Direction _digSide;
    private int _score;
    private int _goldLeft;
    private bool _alive = true;

    private Pass(GameState before)
    {
        _before = before;
        _level = before.Level;
        _holes = [.. before.Holes];
        _score = before.Score;
        _goldLeft = before.GoldLeft;

        var player = before.Player;
        (_column, _row) = player.Position;
        (_offsetX, _offsetY) = (player.OffsetX, player.OffsetY);
        (_pose, _facing, _falling) = (player.Pose, player.Facing, player.Falling);
        (_digging, _digPasses, _digSide) = (player.Digging, player.DigPasses, player.DigSide);
    }

    public static GameState Run(GameState before, Joystick joystick)
    {
        var pass = new Pass(before);
        pass.MovePlayer(joystick);
        pass.ShowExitLadders();

        var completed = pass._row == 0 && pass._offsetY == 0 && pass._goldLeft == 0;
        if (!completed)
            pass.CountDownHoles();

        return pass.Result(completed);
    }

    private GameState Result(bool completed) => new(
        _level,
        new Player(
            new TilePosition(_column, _row), _offsetX, _offsetY, _pose, _facing, _falling, _digging, _digPasses, _digSide),
        _before.Guards,
        _holes,
        _before.Lives,
        _score,
        _goldLeft,
        _alive,
        completed);

    private void MovePlayer(Joystick joystick)
    {
        // A dig and a fall go on whatever the joystick does.
        if (_digging is not null)
        {
            Dig();
            return;
        }

        _falling = Falls();
        if (_falling)
        {
            Fall();
            return;
        }

        // Fire comes before the directions, also when the dig isn't possible.
        if (joystick.HasFlag(Joystick.Fire))
        {
            (_digging, _digPasses) = (_digSide, 0);
            Dig();
            return;
        }

        // Held diagonally, he climbs if he can, and otherwise moves to the side.
        if (joystick.HasFlag(Joystick.Down) ? ClimbDown() : joystick.HasFlag(Joystick.Up) && ClimbUp())
            return;

        if (joystick.HasFlag(Joystick.Right))
            MoveTo(Direction.Right);
        else if (joystick.HasFlag(Joystick.Left))
            MoveTo(Direction.Left);
    }

    // A ladder holds him, and so does a rope he's level with. Anywhere else he needs something to stand on.
    private bool Falls()
    {
        var on = _level[_column, _row];
        if (on == Tile.Ladder || (on == Tile.Rope && _offsetY == 0))
            return false;

        if (_offsetY < 0)
            return true;

        return _row < Level.Height - 1 && TileNow(_column, _row + 1) is not (Tile.Brick or Tile.Solid or Tile.Ladder);
    }

    private void Fall()
    {
        CentreX();
        StepDown();
        (_pose, _facing) = (Pose.Fall, _digSide);
    }

    private void MoveTo(Direction side)
    {
        // A wall stops him in the middle of his tile.
        var step = side == Direction.Right ? 1 : -1;
        if (_offsetX * step >= 0 && (_column + step is < 0 or >= Level.Width || IsWall(TileNow(_column + step, _row))))
            return;

        _digSide = side;
        CentreY();
        _offsetX += step;
        if (Math.Abs(_offsetX) <= MaxOffset)
        {
            TakeGold();
        }
        else
        {
            _offsetX = -MaxOffset * step;
            _column += step;
        }

        (_pose, _facing) = (_level[_column, _row] == Tile.Rope ? Pose.Rope : Pose.Run, side);
    }

    private bool ClimbUp()
    {
        if (_level[_column, _row] == Tile.Ladder)
        {
            if (_offsetY <= 0 && (_row == 0 || IsWall(TileNow(_column, _row - 1))))
                return false;
        }
        else if (_offsetY <= 0 || _row == Level.Height - 1 || _level[_column, _row + 1] != Tile.Ladder)
        {
            return false; // only a little way down from the top of a ladder, he can climb back up
        }

        CentreX();
        _offsetY--;
        if (_offsetY >= -MaxOffset)
        {
            TakeGold();
        }
        else
        {
            _offsetY = MaxOffset;
            _row--;
        }

        (_pose, _facing) = (Pose.Ladder, null);
        return true;
    }

    // Down a ladder, and also off a rope.
    private bool ClimbDown()
    {
        if (_offsetY >= 0 && (_row == Level.Height - 1 || TileNow(_column, _row + 1) is Tile.Brick or Tile.Solid))
            return false;

        CentreX();
        StepDown();
        (_pose, _facing) = (Pose.Ladder, null);
        return true;
    }

    private void StepDown()
    {
        _offsetY++;
        if (_offsetY <= MaxOffset)
        {
            TakeGold();
        }
        else
        {
            _offsetY = -MaxOffset;
            _row++;
        }
    }

    private void Dig()
    {
        // Looked at again in every pass: the tile beside him must be empty, with a brick below it.
        var side = _digging!.Value;
        var column = _column + (side == Direction.Right ? 1 : -1);
        if (_row == Level.Height - 1 || column is < 0 or >= Level.Width
            || TileNow(column, _row + 1) != Tile.Brick || TileNow(column, _row) != Tile.Empty)
        {
            (_digging, _digPasses) = (null, 0);
            return;
        }

        CentreX();
        CentreY();
        (_pose, _facing) = (_digPasses < PassesInDigPose ? Pose.Dig : Pose.Run, side);
        if (_digPasses < PassesBeforeHole)
        {
            _digPasses++;
            return;
        }

        (_digging, _digPasses) = (null, 0);
        _holes.Add(new Hole(new TilePosition(column, _row + 1), Hole.OpenPasses));
    }

    private void CentreX()
    {
        if (_offsetX == 0)
            return;

        _offsetX -= Math.Sign(_offsetX);
        TakeGold();
    }

    private void CentreY()
    {
        if (_offsetY == 0)
            return;

        _offsetY -= Math.Sign(_offsetY);
        TakeGold();
    }

    private void TakeGold()
    {
        if (_offsetX != 0 || _offsetY != 0 || _level[_column, _row] != Tile.Gold)
            return;

        _level = _level.With(_column, _row, Tile.Empty);
        _score += ScoreForGold;
        _goldLeft--;
    }

    private void ShowExitLadders()
    {
        if (_goldLeft > 0 || _level.HiddenLadders.Count == 0)
            return;

        var hidden = _level.HiddenLadders;
        _level = _level.Select(
            (position, tile) => tile == Tile.Empty && hidden.Contains(position) ? Tile.Ladder : tile);
    }

    private void CountDownHoles()
    {
        for (var i = _holes.Count - 1; i >= 0; i--)
        {
            var hole = _holes[i];
            if (hole.PassesLeft > 1)
            {
                _holes[i] = hole with { PassesLeft = hole.PassesLeft - 1 };
                continue;
            }

            _holes.RemoveAt(i);
            if (hole.Position == new TilePosition(_column, _row))
                _alive = false;
        }
    }

    // What's on the tile for the player: a hole is the same to him as an empty tile.
    private Tile TileNow(int column, int row)
    {
        foreach (var hole in _holes)
            if (hole.Position == new TilePosition(column, row))
                return Tile.Empty;

        return _level[column, row];
    }

    private static bool IsWall(Tile tile) => tile is Tile.Brick or Tile.Solid or Tile.TrapBrick;
}

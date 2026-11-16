namespace C64Bot.LodeRunner;

/// <summary>
/// What Lode Runner is doing at the moment, as far as the bot cares: showing itself off, or a level of a real game
/// starting, being played, or ending.
/// </summary>
public enum GamePhase
{
    /// <summary>
    /// None of the others: the game isn't running yet (loading, the trainer menu), or it's in the level editor.
    /// </summary>
    Other,

    /// <summary>
    /// The title screen, before the demo starts.
    /// </summary>
    Title,

    /// <summary>
    /// The game playing itself. SPACE starts a real game.
    /// </summary>
    Demo,

    /// <summary>
    /// Between two stretches of <see cref="Playing"/>: the score counted up after a completed level, the next
    /// level set up and drawn, then waiting for the player's first move.
    /// </summary>
    /// <remarks>
    /// RAM doesn't tell these apart. The game only reads the joystick once the level is ready, so a first move
    /// held during this phase counts as soon as it can. That a level was completed shows in the level number
    /// (<see cref="RamMap.LevelNumber"/>), which goes up as soon as it is.
    /// </remarks>
    LevelStarting,

    /// <summary>
    /// The level is running: the player and the guards move.
    /// </summary>
    Playing,

    /// <summary>
    /// The player has died during play and has lives left; the level starts again.
    /// </summary>
    Dying,

    /// <summary>
    /// The player has died with no lives left: the game-over sign, then the high-score list.
    /// </summary>
    /// <remarks>
    /// This starts with the death of the last life, and also covers the high-score list shown after the demo.
    /// </remarks>
    GameOver,
}

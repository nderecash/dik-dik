using System;

namespace Dikdik.Commands
{
    /// <summary>
    /// A source of player commands.
    ///
    /// Keyboard and voice each implement this. Everything downstream of the bus
    /// receives an <see cref="Intent"/> and cannot tell which producer made it.
    /// That is the whole design argument of this project, expressed as an interface:
    /// there is no normal input and no alternative input, only producers.
    ///
    /// Rule for adding features: a command is not finished until every producer
    /// can raise it. Ship both in the same commit or the feature is not done.
    /// </summary>
    public interface ICommandProducer
    {
        /// <summary>Raised whenever this producer resolves player input into a command.</summary>
        event Action<Intent> CommandProduced;

        /// <summary>
        /// False when this producer cannot run here, for example voice in a WebGL build
        /// or a machine with no microphone. The game reports this in settings and
        /// carries on. It never treats an unavailable producer as a failure state.
        /// </summary>
        bool IsAvailable { get; }

        /// <summary>Player facing name, shown in the settings screen.</summary>
        string DisplayName { get; }
    }
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Threading.Tasks;

namespace StatsdClient
{
    public interface IAllowsDelta { }

    public interface IAllowsDouble { }

    public interface IAllowsInteger { }

    public interface IAllowsSampleRate { }

    public interface IAllowsString { }

    public class Statsd : IStatsd
    {
        #region Private Fields

        private readonly object _commandCollectionLock = new object();

        private readonly Dictionary<Type, string> _commandToUnit = new Dictionary<Type, string>
                                                                       {
                                                                           {typeof (Counting), "c"},
                                                                           {typeof (Timing), "ms"},
                                                                           {typeof (Gauge), "g"},
                                                                           {typeof (Histogram), "h"},
                                                                           {typeof (Meter), "m"},
                                                                           {typeof (Set), "s"}
                                                                       };

        private readonly string _prefix;

        #endregion Private Fields

        #region Public Constructors

        public Statsd(IMetricsSender sender, IRandomGenerator randomGenerator, IStopWatchFactory stopwatchFactory, string prefix)
        {
            if (sender == null) throw new ArgumentNullException("sender");
            if (randomGenerator == null) throw new ArgumentNullException("randomGenerator");
            if (stopwatchFactory == null) throw new ArgumentNullException("stopwatchFactory");
            Commands = new List<string>();
            StopwatchFactory = stopwatchFactory;
            Sender = sender;
            RandomGenerator = randomGenerator;
            _prefix = prefix.EndsWith(".") ? prefix : prefix + ".";
        }

        public Statsd(IMetricsSender udp, IRandomGenerator randomGenerator, IStopWatchFactory stopwatchFactory)
            : this(udp, randomGenerator, stopwatchFactory, string.Empty)
        { }

        public Statsd(IMetricsSender udp, string prefix)
            : this(udp, new RandomGenerator(), new StopWatchFactory(), prefix)
        { }

        public Statsd(IMetricsSender udp)
            : this(udp, "")
        { }

        #endregion Public Constructors

        #region Public Properties

        public List<string> Commands { get; private set; }

        #endregion Public Properties

        #region Private Properties

        private IRandomGenerator RandomGenerator { get; set; }
        private IMetricsSender Sender { get; set; }
        private IStopWatchFactory StopwatchFactory { get; set; }

        #endregion Private Properties

        #region Public Methods

        public void Add<TCommandType>(string name, string value) where TCommandType : IAllowsString
        {
            ThreadSafeAddCommand(GetCommand(name, value.ToString(CultureInfo.InvariantCulture),
                _commandToUnit[typeof(TCommandType)], 1));
        }

        public void Add<TCommandType>(string name, int value) where TCommandType : IAllowsInteger
        {
            ThreadSafeAddCommand(GetCommand(name, value.ToString(CultureInfo.InvariantCulture),
                _commandToUnit[typeof(TCommandType)], 1));
        }

        public void Add<TCommandType>(string name, double value) where TCommandType : IAllowsDouble
        {
            ThreadSafeAddCommand(GetCommand(name, string.Format(CultureInfo.InvariantCulture, "{0:F15}", value),
                _commandToUnit[typeof(TCommandType)], 1));
        }

        public void Add<TCommandType>(string name, double value, bool isDelta)
            where TCommandType : IAllowsDouble, IAllowsDelta
        {
            var prefix = GetDeltaPrefix(value, isDelta);
            ThreadSafeAddCommand(GetCommand(name, string.Format(CultureInfo.InvariantCulture,
                "{0}{1:F15}", prefix, value), _commandToUnit[typeof(TCommandType)], 1));
        }

        public void Add<TCommandType>(string name, int value, double sampleRate)
            where TCommandType : IAllowsInteger, IAllowsSampleRate
        {
            if (RandomGenerator.ShouldSend(sampleRate))
            {
                Commands.Add(GetCommand(name, value.ToString(CultureInfo.InvariantCulture),
                    _commandToUnit[typeof(TCommandType)], sampleRate));
            }
        }

        public void Add(Action actionToTime, string statName, double sampleRate = 1)
        {
            var stopwatch = StopwatchFactory.Get();

            try
            {
                stopwatch.Start();
                actionToTime();
            }
            finally
            {
                stopwatch.Stop();
                if (RandomGenerator.ShouldSend(sampleRate))
                {
                    Add<Timing>(statName, stopwatch.ElapsedMilliseconds());
                }
            }
        }

        public void Send<TCommandType>(string name, int value) where TCommandType : IAllowsInteger
        {
            Commands = new List<string>
            {
                GetCommand(name, value.ToString(CultureInfo.InvariantCulture), _commandToUnit[typeof(TCommandType)], 1)
            };
            Send().WaitAndUnwrapException();
        }

        public void Send<TCommandType>(string name, double value) where TCommandType : IAllowsDouble
        {
            Commands = new List<string>
            {
                GetCommand(name, string.Format(CultureInfo.InvariantCulture,"{0:F15}", value),
                _commandToUnit[typeof(TCommandType)], 1)
            };
            Send().WaitAndUnwrapException();
        }

        public void Send<TCommandType>(string name, string value) where TCommandType : IAllowsString
        {
            Commands = new List<string>
            {
                GetCommand(name, value.ToString(CultureInfo.InvariantCulture), _commandToUnit[typeof(TCommandType)], 1)
            };
            Send().WaitAndUnwrapException();
        }

        public void Send<TCommandType>(string name, int value, double sampleRate)
            where TCommandType : IAllowsInteger, IAllowsSampleRate
        {
            if (RandomGenerator.ShouldSend(sampleRate))
            {
                Commands = new List<string>
                {
                    GetCommand(name, value.ToString(CultureInfo.InvariantCulture),
                    _commandToUnit[typeof(TCommandType)], sampleRate)
                };
                Send().WaitAndUnwrapException();
            }
        }

        public void Send<TCommandType>(string name, double value, bool isDelta)
            where TCommandType : IAllowsDouble, IAllowsDelta
        {
            var prefix = GetDeltaPrefix(value, isDelta);
            Commands = new List<string>
            {
                GetCommand(name, prefix + value.ToString(CultureInfo.InvariantCulture),
                    _commandToUnit[typeof(TCommandType)], 1)
            };
            Send().WaitAndUnwrapException();
        }

        public async Task Send()
        {
            try
            {
                await Sender.Send(string.Join("", Commands.ToArray()));
                Commands = new List<string>(); // only reset our list if it made it through properly!
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.Message);
                throw; // let the caller handle errors
            }
        }

        public void Send(Action actionToTime, string statName, double sampleRate = 1)
        {
            var stopwatch = StopwatchFactory.Get();

            try
            {
                stopwatch.Start();
                actionToTime();
            }
            finally
            {
                stopwatch.Stop();
                if (RandomGenerator.ShouldSend(sampleRate))
                {
                    Send<Timing>(statName, stopwatch.ElapsedMilliseconds());
                }
            }
        }

        #endregion Public Methods

        #region Private Methods

        private string GetCommand(string name, string value, string unit, double sampleRate)
        {
            var format = sampleRate == 1 ? "{0}:{1}|{2}\n" : "{0}:{1}|{2}|@{3}\n";
            return string.Format(CultureInfo.InvariantCulture, format, _prefix + name, value, unit, sampleRate);
        }

        private string GetDeltaPrefix(double value, bool isDelta)
        {
            return isDelta && value >= 0 ? "+" : string.Empty;
        }

        private void ThreadSafeAddCommand(string command)
        {
            lock (_commandCollectionLock)
            {
                Commands.Add(command);
            }
        }

        #endregion Private Methods

        #region Public Classes

        public class Counting : IAllowsSampleRate, IAllowsInteger { }

        public class Gauge : IAllowsDouble, IAllowsDelta { }

        public class Histogram : IAllowsInteger { }

        public class Meter : IAllowsInteger { }

        public class Set : IAllowsString { }

        public class Timing : IAllowsSampleRate, IAllowsInteger { }

        #endregion Public Classes
    }
}
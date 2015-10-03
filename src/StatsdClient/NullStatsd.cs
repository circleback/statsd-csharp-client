using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace StatsdClient
{
    public class NullStatsd : IStatsd
    {
        public NullStatsd()
        {
            Commands = new List<string>();
        }

        public List<string> Commands { get; private set; }

        public void Send<TCommandType>(string name, int value) where TCommandType : IAllowsInteger
        {
        }

        public void Add<TCommandType>(string name, int value) where TCommandType : IAllowsInteger
        {
        }

        public void Send<TCommandType>(string name, double value) where TCommandType : IAllowsDouble
        {
        }

        public void Add<TCommandType>(string name, double value) where TCommandType : IAllowsDouble
        {
        }

        public void Send<TCommandType>(string name, int value, double sampleRate)
            where TCommandType : IAllowsInteger, IAllowsSampleRate
        {
        }

        public void Add<TCommandType>(string name, int value, double sampleRate)
            where TCommandType : IAllowsInteger, IAllowsSampleRate
        {
        }

        public void Send<TCommandType>(string name, string value) where TCommandType : IAllowsString
        {
        }

        public async Task Send()
        {
            await Task.Delay(0);
        }

        public void Add(Action actionToTime, string statName, double sampleRate = 1)
        {
            actionToTime();
        }

        public void Send(Action actionToTime, string statName, double sampleRate = 1)
        {
            actionToTime();
        }

        public void Add<TCommandType>(string name, double value, bool isDelta) where TCommandType : IAllowsDouble, IAllowsDelta
        {
        }

        public void Send<TCommandType>(string name, double value, bool isDelta) where TCommandType : IAllowsDouble, IAllowsDelta
        {
        }

        public void Add<TCommandType>(string name, string value) where TCommandType : IAllowsString
        {
            
        }
    }
}
using System;
using System.Collections.Generic;
using UnityEngine;
namespace IngameDebugConsole
{
    public class CommandHistorySaveManager
	{
		private CircularBuffer<string> commandHistory;
		private int commandHistorySize;
		private string commandHistorySaveSeparator = ";";
		private string commandHistorySaveKey;


        private List<string> tempBufferValues;


        public CommandHistorySaveManager(CircularBuffer<string> commandHistory, int commandHistorySize, string commandHistorySaveSeparator, string commandHistorySaveKey)
        {
            this.commandHistory = commandHistory;
            this.commandHistorySize = commandHistorySize;
            this.commandHistorySaveSeparator = commandHistorySaveSeparator;
			this.commandHistorySaveKey = commandHistorySaveKey;

			tempBufferValues = new();
        }

        public void SaveCommands(CircularBuffer<string> currentCommandHistory) 
		{
			tempBufferValues.Clear();
			for (int i = 0; i < currentCommandHistory.Count; i++)
			{
				tempBufferValues.Add(currentCommandHistory[i]);
			}

			string newCommandHistorySave = string.Join(commandHistorySaveSeparator, tempBufferValues.ToArray());
			PlayerPrefs.SetString(commandHistorySaveKey, newCommandHistorySave);
			PlayerPrefs.Save();
		}

        public void LoadAllCommandsToCommandHistory()
		{
            string commandHistorySave = PlayerPrefs.GetString(commandHistorySaveKey);

            string[] loadedCommands = commandHistorySave.Split(commandHistorySaveSeparator);

			LoadCommandsIntoBuffer(loadedCommands, commandHistory, commandHistorySize);
        }

		private void LoadCommandsIntoBuffer(string[] commands, CircularBuffer<string> buffer, int capacity)
		{
			int loadCount = Math.Min(commands.Length, capacity);

            for (int i = 0; i < loadCount; i++)
            {
                buffer.Add(commands[i]);
            }
        }
    }
}
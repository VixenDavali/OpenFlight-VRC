﻿/**
 * @ Maintainer: Happyrobot33
 */

using UnityEngine;
using UdonSharp;
using OpenFlightVRC.UI;
using VRC.SDK3.Data;
using TMPro;

namespace OpenFlightVRC
{
	/// <summary>
	/// The type of log to write
	/// </summary>
	[System.Flags]
	public enum LogLevel
	{
		Info = 1 << 0,
		/// <summary>
		/// Used for logging where a callback is induced or setup, like <see cref="Info"/> but specifically for callbacks
		/// This should only be used for info level like messages
		/// Actual Callback errors or warnings should be logged as <see cref="Error"/> or <see cref="Warning"/> respectively
		/// </summary>
		Callback = 1 << 1,
		Warning = 1 << 2,
		Error = 1 << 3
	};

	/// <summary>
	/// A simple logger that prefixes all messages with [OpenFlight]
	/// </summary>
	[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
	[DefaultExecutionOrder(-1000)] //Ensure this runs before any other scripts so its name is correct
	public class Logger : UdonSharpBehaviour
	{
		/// <summary>
		/// The name of the log object
		/// </summary>
		const string logObjectName = "OpenFlightLogObject";

		#region In-Client Log Visualisation
		public string log = "";
		public DataDictionary logDictionary = new DataDictionary();
		public TextMeshProUGUI text;

		void Start()
		{
			//set our name just to be sure its correct
			gameObject.name = logObjectName;

			//TODO: Remove this, but this is testing calls for toggles
			SetControlMatrix("PlayerSettings", Util.OrEnums(LogLevel.Info, LogLevel.Warning, LogLevel.Error));
			SetControlMatrix("PlayerMetrics", Util.OrEnums(LogLevel.Info, LogLevel.Callback, LogLevel.Error));
		}

		/// <summary>
		/// Updates the log text
		/// </summary>
		private void UpdateLog()
		{
			text.text = log;
		}
		#endregion

		#region Public Logging API
		const string PackageColor = "orange";
		const string PackageName = "OpenFlight";
		/// <summary>
		/// The max number of log messages to display
		/// </summary>
		const int MaxLogMessages = 200;

		public void SetControlMatrix(string category, LogLevel levels)
		{
			DataDictionary categoryDict = new DataDictionary();
			if (logDictionary.TryGetValue(category, out DataToken levelToken))
			{
				//token is a dictionary of the different log levels
				categoryDict = levelToken.DataDictionary;
			}

			//if the category does not exist, create it
			categoryDict.SetValue("logLevelFlags", new DataToken(System.Convert.ToInt64(levels)));

			//make sure the dictionary has the key
			logDictionary.SetValue(category, categoryDict);
		}

		internal static void WriteToUILog(string text, LogLevel level, LoggableUdonSharpBehaviour self)
		{
			Logger logProxy = null;
			if (!SetupLogProxy(self, ref logProxy))
			{
				return;
			}

			//do our work on the list
			DataDictionary logEntry = new DataDictionary();
			logEntry.SetValue("text", text);
			logEntry.SetValue("time", System.DateTime.Now.Ticks);
			//TODO: Re-enable this when not debugging the outputted json structure using string
			//logEntry.SetValue("script", self);

			//if self is null, then use a static string
			string logCategory = "Utility Methods";
			if (self != null)
			{
				logCategory = self._logCategory;
			}

			if (logCategory != null)
			{
				DataDictionary categoryDict = new DataDictionary();
				DataList logList = new DataList();
				//get the value
				if (logProxy.logDictionary.TryGetValue(logCategory, out DataToken levelToken))
				{
					//token is a dictionary of the different log levels
					categoryDict = levelToken.DataDictionary;

					if (categoryDict.TryGetValue(LogTypeToString(level), out DataToken logToken))
					{
						logList = logToken.DataList;
					}
				}
				else
				{
					//if the category does not exist, create it
					logProxy.logDictionary.SetValue(logCategory, categoryDict);
				}


				//add the entry to the list
				logList.Add(logEntry);

				//update the key
				categoryDict.SetValue(LogTypeToString(level), logList);

				//print out the entire json
				if (VRCJson.TrySerializeToJson(logProxy.logDictionary, JsonExportType.Minify, out DataToken jsonData))
				{
					Debug.Log(jsonData.String);
				}
				else
				{
					Debug.LogError("Could not serialize log dictionary to json! " + jsonData.Error);
				}
			}
			else
			{
				Debug.LogError("Log category is null, cannot log to UI!");
			}

			/* //add the text to the log
			logProxy.log += text + "\n";

			//split into lines
			//trim the log if it is too long
			string[] lines = logProxy.log.Split('\n');
			if (lines.Length > MaxLogMessages)
			{
				logProxy.log = string.Join("\n", lines, lines.Length - MaxLogMessages, MaxLogMessages);
			}

			logProxy.UpdateLog(); */
		}

		/// <summary>
		/// Gets the log type string
		/// </summary>
		/// <param name="lT"></param>
		/// <returns></returns>
		private static string GetLogTypeString(LogLevel lT)
		{
			switch (lT)
			{
				case LogLevel.Info:
					return ColorText(nameof(LogLevel.Info), "white");
				case LogLevel.Callback:
					return ColorText(nameof(LogLevel.Callback), "cyan");
				case LogLevel.Warning:
					return ColorText(nameof(LogLevel.Warning), "yellow");
				case LogLevel.Error:
					return ColorText(nameof(LogLevel.Error), "red");
				default:
					return "";
			}
		}

		/// <summary>
		/// Converts a LogType to a string
		/// </summary>
		/// <param name="LT"></param>
		/// <returns></returns>
		private static string LogTypeToString(LogLevel LT)
		{
			switch (LT)
			{
				case LogLevel.Info:
					return nameof(LogLevel.Info);
				case LogLevel.Callback:
					return nameof(LogLevel.Callback);
				case LogLevel.Warning:
					return nameof(LogLevel.Warning);
				case LogLevel.Error:
					return nameof(LogLevel.Error);
				default:
					return "";
			}
		}

		/// <summary>
		/// Logs a message to the console
		/// </summary>
		/// <param name="text"></param>
		/// <param name="LT"></param>
		private static void LogToConsole(string text, LogLevel LT)
		{
			switch (LT)
			{
				case LogLevel.Info:
					Debug.Log(text);
					break;
				case LogLevel.Callback:
					Debug.Log(text);
					break;
				case LogLevel.Warning:
					Debug.LogWarning(text);
					break;
				case LogLevel.Error:
					Debug.LogError(text);
					break;
			}
		}

		/// <summary>
		/// Sets up the log proxy system
		/// </summary>
		/// <param name="self"></param>
		/// <param name="Logger"></param>
		/// <returns> Whether or not the setup was successful </returns>
		private static bool SetupLogProxy(LoggableUdonSharpBehaviour self, ref Logger Logger)
		{
			//check if self is null
			//if it isnt, we can check for and setup the logproxy cache system
			if (self != null)
			{
				Logger = self._logProxy;

				if (Logger == null)
				{

					GameObject logObject = GameObject.Find(logObjectName);

					if (logObject == null)
					{
						return false;
					}

					Logger logUdon = logObject.GetComponent<Logger>();

					self._logProxy = logUdon;
					Logger = logUdon;
				}
			}
			else
			{
				//if it *is* null, we need to do the more expensive gameobject.find every time
				GameObject logObject = GameObject.Find(logObjectName);

				if (logObject == null)
				{
					return false;
				}

				Logger logUdon = logObject.GetComponent<Logger>();

				Logger = logUdon;
			}

			return true;
		}

		/// <summary>
		/// Logs a message to the console
		/// </summary>
		/// <param name="level">The level of the log</param>
		/// <param name="text">The text to print to the console</param>
		/// <param name="once">Whether or not to only log the message once and ignore future calls until the latest message is different</param>
		/// <param name="self">The UdonSharpBehaviour that is logging the text</param>
		internal static void Log(LogLevel level, string text, bool once = false, LoggableUdonSharpBehaviour self = null)
		{
			//check if the message has already been logged
			if (once && CheckIfLogged(text, self))
			{
				return;
			}

			LogToConsole(Format(text, level, self), level);
			//WriteToUILog(Format(text, LogLevel.Info, self, false), self);
			WriteToUILog(text, level, self);
		}

		/// <summary>
		/// Checks if a specific text has been logged already, as the latest message
		/// </summary>
		/// <param name="text"></param>
		/// <param name="self"></param>
		/// <returns> Whether or not the text has been logged as the latest message </returns>
		internal static bool CheckIfLogged(string text, LoggableUdonSharpBehaviour self)
		{
			Logger logProxy = null;
			if (!SetupLogProxy(self, ref logProxy))
			{
				return false;
			}

			string logString = logProxy.log;

			//check if the latest message is the same as the text
			return logString.EndsWith(text + "\n");
		}

		/// <summary>
		/// Gets the current timestamp
		/// </summary>
		/// <returns></returns>
		private static string GetTimeStampString()
		{
			string time = System.DateTime.Now.ToString("T");
			return ColorText(time, "white");
		}

		/// <summary>
		/// Formats the text to be logged
		/// </summary>
		/// <param name="text">The text to format</param>
		/// <param name="self">The UdonSharpBehaviour that is logging the text</param>
		/// <param name="includePrefix">Whether or not to include the prefix</param>
		/// <returns>The formatted text</returns>
		internal static string Format(string text, LogLevel LT, UdonSharpBehaviour self, bool includePrefix = true)
		{
			string prefix = includePrefix ? string.Format("[{0}]", ColorText(PackageName, PackageColor)) : "";
			return string.Format("{0} [{1}] [{2}] [{3}] {4}", prefix, GetLogTypeString(LT), GetTimeStampString(), ColorizeScript(self), text);
			//return string.Format("{0} [{1}] {2}", prefix, ColorizeScript(self), text);
			//return (includePrefix ? Prefix() + " " : "") + ColorizeScript(self) + " " + text;
		}

		/// <summary>
		/// Returns a colored string of the UdonSharpBehaviour's name
		/// </summary>
		/// <param name="script">The UdonSharpBehaviour to colorize</param>
		/// <returns>The colored name</returns>
		public static string ColorizeScript(UdonSharpBehaviour script)
		{
			return ColorText(GetScriptName(script), ChooseColor(script));
		}

		/// <summary>
		/// Returns a colored string of the UdonSharpBehaviour's function
		/// </summary>
		/// <param name="script">The UdonSharpBehaviour to colorize</param>
		/// <param name="function">The function to colorize</param>
		/// <returns>The colored function</returns>
		public static string ColorizeFunction(UdonSharpBehaviour script, string function)
		{
			string colorized = ColorText(function, ChooseColor(script));

			//italicise it to denote that it is a function
			return string.Format("<i>{0}</i>", colorized);
		}

		/// <summary>
		/// Colors a string
		/// </summary>
		/// <param name="text">The text to color</param>
		/// <param name="color">The color to color the text</param>
		/// <returns>The colored text</returns>
		private static string ColorText(string text, string color)
		{
			return string.Format("<color={0}>{1}</color>", color, text);
		}

		/// <summary>
		/// Chooses a color based on the name of the UdonSharpBehaviour
		/// </summary>
		/// <param name="self">The UdonSharpBehaviour to choose a color for</param>
		/// <returns>The color to use</returns>
		private static string ChooseColor(UdonSharpBehaviour self)
		{
			//if the script is null, init to a constant
			if (self == null)
			{
				Random.InitState(0);
			}
			else
			{
				//set random seed to hash of name
				Random.InitState(GetScriptName(self).GetHashCode());
			}

			float Saturation = 1f;
			float Brightness = 1f;

			float hue = Random.Range(0.0f, 1.0f);

			Color color = Color.HSVToRGB(hue, Saturation, Brightness);

			return ColorToHTML(color);
		}

		/// <summary>
		/// Converts RGB to HTML
		/// </summary>
		/// <param name="color">The color to convert</param>
		/// <returns>The HTML color</returns>
		private static string ColorToHTML(Color color)
		{
			string RHex = ((int)(color.r * 255)).ToString("X2");
			string GHex = ((int)(color.g * 255)).ToString("X2");
			string BHex = ((int)(color.b * 255)).ToString("X2");

			return "#" + RHex + GHex + BHex;
		}

		/// <summary>
		/// Gets the name of the UdonSharpBehaviour. If null, returns "Untraceable Static Function Call"
		/// </summary>
		/// <param name="script"></param>
		/// <returns></returns>
		private static string GetScriptName(UdonSharpBehaviour script)
		{
			//check if null
			if (script == null)
			{
				return "Untraceable Static Function Call";
			}

			return script.name;
		}
		#endregion
	}
}

using UnityEngine;
using System;
using UnityEditor;

public static class DebugTimer {
	private static float _startTime;

	public static void initialize()
	{
		_startTime = Time.time;
	}

	public static string elapsedTime {
		get {
			TimeSpan timeSpan = TimeSpan.FromSeconds(Time.time - _startTime);
			return string.Format("{0:D2}:{1:D2}:{2:D2}:{3:D3}", timeSpan.Hours, timeSpan.Minutes, timeSpan.Seconds, timeSpan.Milliseconds);
		}
	}
}

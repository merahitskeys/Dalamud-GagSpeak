using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Timers;

namespace GagSpeak.Services;

// TimerService class manages timers and notifies when remaining time changes
public class TimerService : IDisposable
{
   // Event to notify subscribers when remaining time changes
   public event Action<string, TimeSpan> RemainingTimeChanged;

   // Dictionary to store active timers
   private readonly Dictionary<string, TimerData> timers = new Dictionary<string, TimerData>();
   
   // creating a dictionary to store a list of times from the timer serivce to display to UI
   public readonly Dictionary<string, string> remainingTimes = new Dictionary<string, string>();

   // Method to start a new timer
   public void StartTimer(string timerName, string input, int elapsedMilliSecPeriod, Action onElapsed) {
      StartTimer(timerName, input, elapsedMilliSecPeriod, onElapsed, null, -1);}
   
   // the augmented constructor for the timer service to handle padlock timers
   public void StartTimer(string timerName, string input,  int elapsedMilliSecPeriod, Action onElapsed,
   List<DateTimeOffset> padlockTimerList, int index) {
      // Check if a timer with the same name already exists
      if (timers.ContainsKey(timerName)) {
         GagSpeak.Log.Debug($"Timer with name '{timerName}' already exists. Use a different name.");
         return;
      }

      // Parse the input string to get the duration
      TimeSpan duration = ParseTimeInput(input);

      // Check if the duration is valid
      if (duration == TimeSpan.Zero){
         GagSpeak.Log.Debug($"Invalid time format for timer '{timerName}'.");
         return;
      }

      // Calculate the end time of the timer
      DateTimeOffset endTime = DateTimeOffset.Now.Add(duration);

      // update the selectedGagPadLockTimer list with the new end time. (only if using a list as input)
      if (padlockTimerList != null && index >= 0 && index < padlockTimerList.Count) {
         padlockTimerList[index] = endTime;
      }

      // Create a new timer
      Timer timer = new Timer(elapsedMilliSecPeriod);
      timer.Elapsed += (sender, args) => OnTimerElapsed(timerName, timer, onElapsed);
      timer.Start();

      // Store the timer data in the dictionary
      timers[timerName] = new TimerData(timer, endTime);

   }

    // Method called when a timer elapses
   private void OnTimerElapsed(string timerName, Timer timer, Action onElapsed) {
      if (timers.TryGetValue(timerName, out var timerData)) {
         // Calculate remaining time
         TimeSpan remainingTime = timerData.EndTime - DateTimeOffset.Now;
         if (remainingTime <= TimeSpan.Zero) {
               // Timer expired
               GagSpeak.Log.Debug($"Timer '{timerName}' expired.");
               timer.Stop();
               onElapsed?.Invoke();
               timers.Remove(timerName);
         }
         else {
               // Notify subscribers about remaining time change
               RemainingTimeChanged?.Invoke(timerName, remainingTime);
         }
      }
   }

   // Method to parse time input string
   public static TimeSpan ParseTimeInput(string input) {
      // Match hours, minutes, and seconds in the input string
      var match = Regex.Match(input, @"^(?:(\d+)h)?(?:(\d+)m)?(?:(\d+)s)?$");

      if (match.Success) { // Parse hours, minutes, and seconds
         int.TryParse(match.Groups[1].Value, out int hours);
         int.TryParse(match.Groups[2].Value, out int minutes);
         int.TryParse(match.Groups[3].Value, out int seconds);
         // Return the total duration
         return new TimeSpan(hours, minutes, seconds);
      }

      // If the input string is not in the correct format, return TimeSpan.Zero
      return TimeSpan.Zero;
   }

    // Nested class to store timer data
   private class TimerData
   {
      public Timer Timer { get; }
      public DateTimeOffset EndTime { get; }

      public TimerData(Timer timer, DateTimeOffset endTime) {
         Timer = timer;
         EndTime = endTime;
      }
   }

   // Method to dispose the service
   public void Dispose()
   {
      // Dispose all timers
      foreach (var timerData in timers.Values) {
         timerData.Timer.Dispose();
      }
   }
}
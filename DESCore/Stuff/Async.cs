﻿#region -- copyright --
//
// Licensed under the EUPL, Version 1.1 or - as soon they will be approved by the
// European Commission - subsequent versions of the EUPL(the "Licence"); You may
// not use this work except in compliance with the Licence.
//
// You may obtain a copy of the Licence at:
// http://ec.europa.eu/idabc/eupl
//
// Unless required by applicable law or agreed to in writing, software distributed
// under the Licence is distributed on an "AS IS" basis, WITHOUT WARRANTIES OR
// CONDITIONS OF ANY KIND, either express or implied. See the Licence for the
// specific language governing permissions and limitations under the Licence.
//
#endregion
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TecWare.DE.Stuff
{
	#region -- class AsyncQueue -------------------------------------------------------

	/// <summary>Start async operations in queue in the current context.</summary>
	public sealed class AsyncQueue
	{
		private readonly int threadId;
		private readonly CancellationToken cancellationToken;
		private Task currentTask = null;
		private readonly Queue<Func<Task>> pendingTasks = new Queue<Func<Task>>();

		/// <summary></summary>
		public AsyncQueue(CancellationToken cancellationToken = default)
		{
			threadId = Thread.CurrentThread.ManagedThreadId;
			this.cancellationToken = cancellationToken;
		} // ctor

		private void CheckThreading()
		{
			if (threadId != Thread.CurrentThread.ManagedThreadId)
				throw new InvalidOperationException();
		} // proc CheckThreading

		private void Start(Func<Task> task)
		{
			if (currentTask != null)
				throw new InvalidOperationException();

			currentTask = task();
			currentTask.ContinueWith(OnFinish, TaskContinuationOptions.ExecuteSynchronously);
		} // proc Start

		private void OnFinish(Task task)
		{
			CheckThreading();

			// start next
			currentTask = null;
			if (pendingTasks.Count > 0 && !cancellationToken.IsCancellationRequested)
				Start(pendingTasks.Dequeue());

			// notify exception
			if (task.IsFaulted)
				OnException?.Invoke(task.Exception);
		} // proc OnFinish

		/// <summary>Start a new task.</summary>
		/// <param name="task"></param>
		public void Enqueue(Func<Task> task)
		{
			CheckThreading();

			// enqueue task for execution
			if (!IsTaskRunning) // first task, execute
				Start(task);
			else // add pending tasks
				pendingTasks.Enqueue(task);
		} // proc Enqueue

		/// <summary>Is a task executed</summary>
		public bool IsTaskRunning => currentTask != null;
		/// <summary>CancellationToken</summary>
		public CancellationToken CancellationToken => cancellationToken;
		/// <summary>Execute if a task failed.</summary>
		public Action<Exception> OnException { get; set; } = null;
	} // class AsyncQueue

	#endregion

	#region -- class SequenceTimer ----------------------------------------------------

	/// <summary>This timer invokes a action after an amound of time. It is ideal 
	/// to delay a invocation.</summary>
	public sealed class SequenceTimer
	{
		/// <summary>Is the flag waiting.</summary>
		public event EventHandler IsEnabledChanged;

		private readonly Action action;
		private int seqToken = 0;
		private bool isEnabled = false;

		/// <summary>Action to invoke.</summary>
		/// <param name="action"></param>
		public SequenceTimer(Action action = null)
		{
			this.action = action;
		} // ctor

		/// <summary>Start the timer.</summary>
		/// <param name="milliseconds"></param>
		public void Start(int milliseconds)
			=> Start(TimeSpan.FromMilliseconds(milliseconds));

		/// <summary>Start the timer.</summary>
		/// <param name="time"></param>
		public void Start(TimeSpan time)
		{
			IsEnabled = true;

			InvokeTimedTokenAsync(time, unchecked(++seqToken)).Silent();
		} // proc Start

		private async Task InvokeTimedTokenAsync(TimeSpan time, int token)
		{
			await Task.Delay(time);
			if (token == seqToken)
			{
				try
				{
					action?.Invoke();
				}
				finally
				{
					IsEnabled = false;
				}
			}
		} // func InvokeTimedTokenAsync

		/// <summary>Stop the timer.</summary>
		public void Stop()
		{
			seqToken++;
			IsEnabled = false;
		} // proc Stop

		/// <summary>Is the timer active.</summary>
		public bool IsEnabled
		{
			get => isEnabled;
			set
			{
				if (isEnabled != value)
				{
					isEnabled = value;
					IsEnabledChanged?.Invoke(this, EventArgs.Empty);
				}
			}
		} // prop IsEnabeld
	} // class SequenceTimer

	#endregion

	#region -- class Procs ------------------------------------------------------------

	public static partial class Procs
	{
		/// <summary></summary>
		/// <param name="task"></param>
		/// <param name="printException"></param>
		public static void Silent(this Task task, Action<Exception> printException = null)
		{
			task.ContinueWith(
				t => 
				{
					if (printException != null)
						printException.Invoke(t.Exception);
					else
						Debug.Print(t.Exception.GetInnerException().ToString());
				},
				TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously
			);
		} // proc Silent
	} // class Procs

	#endregion
}

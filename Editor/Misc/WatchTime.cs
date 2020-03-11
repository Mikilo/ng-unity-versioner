using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace NGUnityVersioner
{
	using UnityEngine;

	public sealed class WatchTime : IDisposable
	{
		private class NestedWatcher
		{
			public string	context;
			public double	time;
		}

		private static Stack<WatchTime>		pool = new Stack<WatchTime>();
		private static Stack<WatchTime>		stack = new Stack<WatchTime>();
		private static Queue<NestedWatcher>	pending = new Queue<NestedWatcher>();

		private Stopwatch	watch = new Stopwatch();
		private string		context;
		private bool		cumulative;
		private long		minimumMilliseconds;

		public static WatchTime	Get(string context, bool cumulative = false, bool nested = false)
		{
			WatchTime	instance;

			lock (WatchTime.pool)
			{
				if (WatchTime.pool.Count > 0)
					instance = WatchTime.pool.Pop();
				else
					instance = new WatchTime();
			}

			if (nested == true)
			{
				instance.context = WatchTime.stack.Peek().context + context;
			}
			else
				instance.context = context;

			instance.cumulative = cumulative;

			instance.watch.Reset();
			instance.watch.Start();

			lock (WatchTime.stack)
			{
				WatchTime.stack.Push(instance);
			}

			return instance;
		}

		private	WatchTime()
		{
		}

		public WatchTime	Set(long minimumMilliseconds)
		{
			this.minimumMilliseconds = minimumMilliseconds;
			return this;
		}

		public void	Dispose()
		{
			int	stackCount;

			lock (WatchTime.stack)
			{
				WatchTime.stack.Pop();
				stackCount = WatchTime.stack.Count;
			}

			this.watch.Stop();

			long	milliseconds = this.watch.ElapsedMilliseconds;

			if (milliseconds >= this.minimumMilliseconds)
			{
				string	output;

				if (milliseconds > 9)
					output = this.context + " (" + milliseconds + " ms)";
				else
					output = this.context + " (" + this.watch.Elapsed.TotalMilliseconds.ToString("F") + " ms)";

				lock (WatchTime.pending)
				{
					if (stackCount == 0)
					{
						foreach (NestedWatcher p in WatchTime.pending)
							Debug.Log(p.context + (p.time > 0D ? " (" + p.time.ToString("F") + " ms)" : string.Empty));
						WatchTime.pending.Clear();

						Debug.Log(output);
					}
					else
					{
						if (this.cumulative == true)
						{
							foreach (var item in WatchTime.pending)
							{
								if (item.context == this.context)
								{
									item.time += this.watch.Elapsed.TotalMilliseconds;
									goto doubleBreak;
								}
							}

							WatchTime.pending.Enqueue(new NestedWatcher() { context = this.context, time = this.watch.Elapsed.TotalMilliseconds });
						}
						else
							WatchTime.pending.Enqueue(new NestedWatcher() { context = output });
					}
				}
			}

			doubleBreak:
			lock (WatchTime.pool)
			{
				WatchTime.pool.Push(this);
			}
		}
	}
}
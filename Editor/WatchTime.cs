using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace NGUnityVersioner
{
	using UnityEngine;

	public sealed class WatchTime : IDisposable
	{
		private static Stack<WatchTime>	pool = new Stack<WatchTime>();

		private Stopwatch	watch = new Stopwatch();
		private string		context;
		private long		minimumMilliseconds;

		public static WatchTime	Get(string context)
		{
			WatchTime	instance;

			if (WatchTime.pool.Count > 0)
				instance = WatchTime.pool.Pop();
			else
				instance = new WatchTime();

			instance.context = context;

			instance.watch.Reset();
			instance.watch.Start();
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
			this.watch.Stop();
			long	milliseconds = this.watch.ElapsedMilliseconds;
			if (milliseconds >= this.minimumMilliseconds)
			{
				if (milliseconds > 1)
					Debug.Log(this.context + " (" + milliseconds + " ms)");
				else
					Debug.Log(this.context + " (" + this.watch.Elapsed.TotalMilliseconds + " ms)");
			}
			WatchTime.pool.Push(this);
		}
	}
}
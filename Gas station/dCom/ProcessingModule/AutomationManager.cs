using Common;
using System;
using System.Collections.Generic;
using System.Threading;

namespace ProcessingModule
{
    /// <summary>
    /// Class containing logic for automated work.
    /// </summary>
    public class AutomationManager : IAutomationManager, IDisposable
	{
		private Thread automationWorker;
        private AutoResetEvent automationTrigger;
        private IStorage storage;
		private IProcessingManager processingManager;
		private int delayBetweenCommands;
        private IConfiguration configuration;


		private const ushort TankAddress = 1000;
		private const ushort ValveAddress = 2000;
		private const ushort Pump1Address = 3001;
		private const ushort Pump2Address = 3002;
		private const ushort Pump3Address = 3003;

		private bool started = false;
        /// <summary>
        /// Initializes a new instance of the <see cref="AutomationManager"/> class.
        /// </summary>
        /// <param name="storage">The storage.</param>
        /// <param name="processingManager">The processing manager.</param>
        /// <param name="automationTrigger">The automation trigger.</param>
        /// <param name="configuration">The configuration.</param>
        public AutomationManager(IStorage storage, IProcessingManager processingManager, AutoResetEvent automationTrigger, IConfiguration configuration)
		{
			this.storage = storage;
			this.processingManager = processingManager;
            this.configuration = configuration;
            this.automationTrigger = automationTrigger;
        }

        /// <summary>
        /// Initializes and starts the threads.
        /// </summary>
		private void InitializeAndStartThreads()
		{
			InitializeAutomationWorkerThread();
			StartAutomationWorkerThread();
		}

        /// <summary>
        /// Initializes the automation worker thread.
        /// </summary>
		private void InitializeAutomationWorkerThread()
		{
			automationWorker = new Thread(AutomationWorker_DoWork);
			automationWorker.Name = "Aumation Thread";
		}

        /// <summary>
        /// Starts the automation worker thread.
        /// </summary>
		private void StartAutomationWorkerThread()
		{
			automationWorker.Start();
		}


		private void AutomationWorker_DoWork()
		{
			List<PointIdentifier> pointsList = new List<PointIdentifier>
			{
				new PointIdentifier(PointType.ANALOG_OUTPUT, TankAddress),
				new PointIdentifier(PointType.DIGITAL_OUTPUT, ValveAddress),
				new PointIdentifier(PointType.DIGITAL_OUTPUT, Pump1Address),
                new PointIdentifier(PointType.DIGITAL_OUTPUT, Pump2Address),
                new PointIdentifier(PointType.DIGITAL_OUTPUT, Pump3Address)
            };

			while (!disposedValue)
			{
				automationTrigger.WaitOne();


				if (disposedValue)
				{
					break;
				}

				List<IPoint> points = storage.GetPoints(pointsList);

				if(points.Count < 5)
				{
					continue;
				}

				IAnalogPoint tank = points[0] as IAnalogPoint;

				if(tank == null)
				{
					continue;
				}

                bool valveOpen = points[1].RawValue == 0;
                bool pump1On = points[2].RawValue == 1;
                bool pump2On = points[3].RawValue == 1;
                bool pump3On = points[4].RawValue == 1;

                int currentLevel = (int)Math.Round(tank.EguValue);
                int newLevel = currentLevel;

                if (valveOpen && !pump1On && !pump2On && !pump3On)
                {
                    // Punjenje kroz U1: 10 l/s
                    newLevel += 10;
                }
                else if (!valveOpen)
                {
                    // Pražnjenje preko pumpi
                    if (pump1On)
                    {
                        newLevel -= 1;
                    }

                    if (pump2On)
                    {
                        newLevel -= 1;
                    }

                    if (pump3On)
                    {
                        newLevel -= 3;
                    }
                }

                if (newLevel < 0)
                {
                    newLevel = 0;
                }

                if (newLevel > 1000)
                {
                    newLevel = 1000;
                }

                if (newLevel != currentLevel)
                {
                    processingManager.ExecuteWriteCommand(
                        tank.ConfigItem,
                        configuration.GetTransactionId(),
                        configuration.UnitAddress,
                        TankAddress,
                        newLevel);

                    if (delayBetweenCommands > 0)
                    {
                        Thread.Sleep(delayBetweenCommands);
                    }
                }
            }
		}

		#region IDisposable Support
		private bool disposedValue = false; // To detect redundant calls


        /// <summary>
        /// Disposes the object.
        /// </summary>
        /// <param name="disposing">Indication if managed objects should be disposed.</param>
		protected virtual void Dispose(bool disposing)
		{
			if (!disposedValue)
			{
				if (disposing)
				{
				}
				disposedValue = true;
			}
		}


		// This code added to correctly implement the disposable pattern.
		public void Dispose()
		{
			// Do not change this code. Put cleanup code in Dispose(bool disposing) above.
			Dispose(true);
			// GC.SuppressFinalize(this);
		}

        /// <inheritdoc />
        public void Start(int delayBetweenCommands)
		{
			this.delayBetweenCommands = delayBetweenCommands*1000;
            InitializeAndStartThreads();
		}

        /// <inheritdoc />
        public void Stop()
		{
			Dispose();
		}
		#endregion
	}
}

using Common;
using System;
using System.Threading;
using System.Collections.Generic;

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

		private const ushort GatePositionAddress = 1000;
		private const ushort ObstacleAddress = 2000;
		private const ushort OpenAddress = 3000;
		private const ushort CloseAddress = 3001;

		private const int Step = 10;

		private bool returningBecauseOfObstacle = false;

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
			while (!disposedValue)
			{
				automationTrigger.WaitOne();

				IAnalogPoint gatePosition = GetPoint(PointType.ANALOG_OUTPUT, GatePositionAddress) as IAnalogPoint;
				IDigitalPoint obstacle = GetPoint(PointType.DIGITAL_INPUT, ObstacleAddress) as IDigitalPoint;
				IDigitalPoint open = GetPoint(PointType.DIGITAL_OUTPUT, OpenAddress) as IDigitalPoint;
				IDigitalPoint close = GetPoint(PointType.DIGITAL_OUTPUT, CloseAddress) as IDigitalPoint;

				if (gatePosition == null || obstacle == null || open == null || close == null)
				{
					continue;
				}

				double currentPosition = gatePosition.EguValue;
				double LowLimit = gatePosition.ConfigItem.LowLimit;
				double highLimit = gatePosition.ConfigItem.HighLimit;

                // Ako je kapija stigla do LowAlarm, Open mora da se ugasi
                if (currentPosition <= LowLimit && open.State == DState.ON)
                {
                    WritePoint(open, OpenAddress, 0);
                    continue;
                }

                // Ako je kapija stigla do HighAlarm, Close mora da se ugasi
                if (currentPosition >= highLimit && close.State == DState.ON)
                {
                    WritePoint(close, CloseAddress, 0);
                    continue;
                }

                // Ako se kapija trenutno vraća zbog prepreke,
                // mora da se vrati skroz do LowAlarm vrednosti
                if (returningBecauseOfObstacle)
                {
                    if (currentPosition > LowLimit)
                    {
                        MoveGate(gatePosition, Math.Max(LowLimit, currentPosition - Step));
                        continue;
                    }

                    // Kapija je stigla do LowAlarm.
                    // Ako prepreka i dalje postoji, čekamo tu.
                    if (obstacle.State == DState.ON)
                    {
                        continue;
                    }

                    // Prepreka je uklonjena.
                    // Pošto Close taster ostaje ON, zatvaranje će se nastaviti ispod.
                    returningBecauseOfObstacle = false;
                }

                // Ako se prilikom zatvaranja pojavi prepreka,
                // pokreni vraćanje kapije ka LowAlarm vrednosti
                if (close.State == DState.ON && obstacle.State == DState.ON)
                {
                    returningBecauseOfObstacle = true;

                    if (currentPosition > LowLimit)
                    {
                        MoveGate(gatePosition, Math.Max(LowLimit, currentPosition - Step));
                    }

                    continue;
                }

                // Normalno otvaranje
                if (open.State == DState.ON)
                {
                    MoveGate(gatePosition, Math.Max(LowLimit, currentPosition - Step));
                }
                // Normalno zatvaranje
                else if (close.State == DState.ON)
                {
                    MoveGate(gatePosition, Math.Min(highLimit, currentPosition + Step));
                }
            }
		}

        private IPoint GetPoint(PointType pointType, ushort address)
        {
            List<IPoint> points = storage.GetPoints(
                new List<PointIdentifier>()
                {
            new PointIdentifier(pointType, address)
                });

            if (points == null || points.Count == 0)
            {
                return null;
            }

            return points[0];
        }

        private void MoveGate(IAnalogPoint gatePosition, double newPosition)
        {
            processingManager.ExecuteWriteCommand(
                gatePosition.ConfigItem,
                configuration.GetTransactionId(),
                configuration.UnitAddress,
                GatePositionAddress,
                (int)Math.Round(newPosition));
        }
        private void WritePoint(IPoint point, ushort address, int value)
        {
            processingManager.ExecuteWriteCommand(
                point.ConfigItem,
                configuration.GetTransactionId(),
                configuration.UnitAddress,
                address,
                value);
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

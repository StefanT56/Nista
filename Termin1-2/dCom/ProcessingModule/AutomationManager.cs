using Common;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Linq;

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

		private const ushort K_ADDRESS = 2000;

		private const ushort I1_ADDRESS = 4000;
		private const ushort I2_ADDRESS = 4001;

        private const ushort T1_ADDRESS = 5000;
        private const ushort T2_ADDRESS = 5001;
        private const ushort T3_ADDRESS = 5002;
        private const ushort T4_ADDRESS = 5003;
        private const ushort T5_ADDRESS = 5004;

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
				new PointIdentifier(PointType.ANALOG_OUTPUT, K_ADDRESS),
				new PointIdentifier(PointType.DIGITAL_OUTPUT, I1_ADDRESS),
                new PointIdentifier(PointType.DIGITAL_OUTPUT, I2_ADDRESS),
                new PointIdentifier(PointType.DIGITAL_OUTPUT, T1_ADDRESS),
                new PointIdentifier(PointType.DIGITAL_OUTPUT, T2_ADDRESS),
                new PointIdentifier(PointType.DIGITAL_OUTPUT, T3_ADDRESS),
                new PointIdentifier(PointType.DIGITAL_OUTPUT, T4_ADDRESS),
                new PointIdentifier(PointType.DIGITAL_OUTPUT, T5_ADDRESS),
            };
			while (!disposedValue)
			{
				automationTrigger.WaitOne();

				if (disposedValue)
				{
					break;
				}

				List<IPoint> points = storage.GetPoints(pointsList);

				if(points.Count < 8)
				{
					continue;
				}

				IAnalogPoint battery = points[0] as IAnalogPoint;

                IDigitalPoint i1 = points[1] as IDigitalPoint;
                IDigitalPoint i2 = points[2] as IDigitalPoint; 

                IDigitalPoint t1 = points[3] as IDigitalPoint;
                IDigitalPoint t2 = points[4] as IDigitalPoint;
                IDigitalPoint t3 = points[5] as IDigitalPoint;
                IDigitalPoint t4 = points[6] as IDigitalPoint;
                IDigitalPoint t5 = points[7] as IDigitalPoint;

                if (battery == null || i1 == null || i2 == null ||
                    t1 == null || t2 == null || t3 == null || t4 == null || t5 == null)
                {
                    continue;
                }

                double capacity = battery.EguValue;
                double lowAlarm = battery.ConfigItem.LowLimit;
                double eguMax = battery.ConfigItem.EGU_Max;

                bool t1On = IsOn(t1);
                bool t2On = IsOn(t2);
                bool t3On = IsOn(t3);
                bool t4On = IsOn(t4);
                bool t5On = IsOn(t5);

                bool i1On = IsOn(i1);
                bool i2On = IsOn(i2);

                if (capacity < lowAlarm)
                {
                    if (t4On)
                    {
                        WritePoint(t4, T4_ADDRESS, 0);
                        t4On = false;
                    }

                    if (t5On)
                    {
                        WritePoint(t5, T5_ADDRESS, 0);
                        t5On = false;
                    }

                    if (!i1On)
                    {
                        WritePoint(i1, I1_ADDRESS, 1);
                        i1On = true;
                    }

                    if (!i2On)
                    {
                        WritePoint(i2, I2_ADDRESS, 1);
                        i2On = true;
                    }
                }

                // Ako je baterija puna, isključi sva napajanja koja su uključena.
                if (capacity >= eguMax)
                {
                    if (i1On)
                    {
                        WritePoint(i1, I1_ADDRESS, 0);
                        i1On = false;
                    }

                    if (i2On)
                    {
                        WritePoint(i2, I2_ADDRESS, 0);
                        i2On = false;
                    }
                }

                int change = 0;

                if (t1On)
                {
                    change -= 1;
                }

                if (t2On)
                {
                    change -= 1;
                }

                if (t3On)
                {
                    change -= 1;
                }

                if (t4On)
                {
                    change -= 3;
                }

                if (t5On)
                {
                    change -= 2;
                }

                if (i1On)
                {
                    change += 3;
                }

                if (i2On)
                {
                    change += 4;
                }

                double newCapacity = capacity + change;

                if (newCapacity < battery.ConfigItem.EGU_Min)
                {
                    newCapacity = battery.ConfigItem.EGU_Min;
                }

                if (newCapacity > eguMax)
                {
                    newCapacity = eguMax;
                }

                if (Math.Abs(newCapacity - capacity) > 0.001)
                {
                    WritePoint(battery, K_ADDRESS, (int)Math.Round(newCapacity));
                }
            }
        }

        private bool IsOn(IDigitalPoint point)
        {
            return point != null && point.RawValue == 1;
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

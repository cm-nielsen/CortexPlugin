using System;
using System.Collections.Generic;
using UnityEngine;

namespace CortexPlugin
{
    /// <summary>
    /// Provides buffered events from active data streams to Unity synchronous events
    /// </summary>
    public class DataSubscriber : MonoBehaviour
    {
        public static DataSubscriber Instance;

        Dictionary<string, DataStreamEventBuffer> dataStreamSubscribers = new Dictionary<string, DataStreamEventBuffer>();
        object mux = new object();

        DataSubscriber()
        {
            if (!Instance)
                Instance = this;
        }

        void Update()
        {
            lock (mux)
                foreach (var subscriber in dataStreamSubscribers.Values)
                    subscriber.Process();
        }

        /// <summary>
        /// Add a new data stream to the event handling process
        /// </summary>
        /// <param name="newStream">Stream to add</param>
        /// <param name="sessionId">id of the corresponding session</param>
        /// <param name="headsetId">id of the relevant headset</param>
        public void AddStream(DataStream newStream, string sessionId, string headsetId)
        {
            try
            {
                lock (mux)
                    dataStreamSubscribers[headsetId] = new DataStreamEventBuffer(newStream, sessionId, headsetId);
                if (Cortex.printLogs)
                    print("New stream added");
            }
            catch (Exception e)
            {
                print(e);
            }
        }
        public void RemoveStreamByHeadsetID(string id)
        {
            lock (mux)
                dataStreamSubscribers.Remove(id);
        }
        public void RemoveStreamBySessionID(string id)
        {
            string toRemove = null;
            foreach (var item in dataStreamSubscribers)
                if (item.Value.sessionId == id)
                    toRemove = item.Key;

            if (!string.IsNullOrEmpty(toRemove))
                lock (mux) dataStreamSubscribers.Remove(toRemove);
        }

        /// <summary>
        /// Checks if a Data Stream currently exists for the given headset
        /// </summary>
        /// <param name="headsetId">ID of desired headset stream</param>
        /// <returns></returns>
        public bool DataStreamExists(string headsetId)
        {
            if (string.IsNullOrEmpty(headsetId))
                return false;
            if (!dataStreamSubscribers.ContainsKey(headsetId))
                return false;
            return true;
        }

        /// <summary>
        /// Connects the provided callback function to the typed
        /// data stream of the given headset, provided it exists.
        /// This callback will be wrapped in Unity's thread and Update callback,
        /// making it able to trigger updates to game state
        /// </summary>
        /// <typeparam name="T">The type of data to subscribe to</typeparam>
        /// <param name="headsetId">ID of the desired headset stream</param>
        /// <param name="callBack">Function to be called</param>
        /// <returns>true if successful</returns>
        public bool SubscribeDataStream<T>(string headsetId, Action<T> callBack) where T : DataStreamEventArgs
        {
            if (string.IsNullOrEmpty(headsetId))
                return false;
            if (!dataStreamSubscribers.ContainsKey(headsetId))
            {
                Debug.LogWarning("DataSubscriber: attempted to Subscribe to a headset stream that doesn't exist");
                return false;
            }
            try
            {
                DataStreamEventBuffer dataStreamSubscriber = dataStreamSubscribers[headsetId];
                switch (typeof(T))
                {
                    case Type mType when mType == typeof(MentalCommand):
                        dataStreamSubscriber.MentalCommandReceived += (Action<MentalCommand>)callBack;
                        break;
                    case Type dType when dType == typeof(DeviceInfo):
                        dataStreamSubscriber.DevDataReceived += (Action<DeviceInfo>)callBack;
                        break;
                    case Type sType when sType == typeof(SystemEventArgs):
                        dataStreamSubscriber.SysEventReceived += (Action<SystemEventArgs>)callBack;
                        break;
                    default:
                        Debug.LogWarning($"Attempted to subscribe to unsupported data stream: {typeof(T)}");
                        return false;
                }
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError(e);
                return false;
            }
        }

        /// <summary>
        /// DiscConnects the provided callback function from the typed
        /// data stream of the given headset, provided it exists.
        /// </summary>
        /// <typeparam name="T">The type of data to subscribe to</typeparam>
        /// <param name="headsetId">ID of the desired headset stream</param>
        /// <param name="callBack">Function to be called</param>
        /// <returns>true if successful</returns>
        public bool UnsubscribeDataStream<T>(string headsetId, Action<T> callBack) where T : DataStreamEventArgs
        {
            if (string.IsNullOrEmpty(headsetId))
                return false;
            if (!dataStreamSubscribers.ContainsKey(headsetId))
            {
                if (!Cortex.isQuitting)
                    Debug.LogWarning("DataSubscriber: attempted to Unsubscribe from a headset stream that doesn't exist");
                return false;
            }

            try
            {
                DataStreamEventBuffer dataStreamSubscriber = dataStreamSubscribers[headsetId];
                switch (typeof(T))
                {
                    case Type mType when mType == typeof(MentalCommand):
                        dataStreamSubscriber.MentalCommandReceived -= (Action<MentalCommand>)callBack;
                        break;
                    case Type dType when dType == typeof(DeviceInfo):
                        dataStreamSubscriber.DevDataReceived -= (Action<DeviceInfo>)callBack;
                        break;
                    case Type sType when sType == typeof(SystemEventArgs):
                        dataStreamSubscriber.SysEventReceived -= (Action<SystemEventArgs>)callBack;
                        break;
                    default:
                        Debug.LogWarning($"Attempted to subscribe to unsupported data stream: {typeof(T)}");
                        return false;
                }
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError(e);
                return false;
            }
        }

    }

    /// <summary>
    /// Specialized Event Buffer than handles desired streams of each open session
    /// You will need to add to this to manage additional data streams
    /// </summary>
    public class DataStreamEventBuffer
    {
        DataStream dataStream;
        public string sessionId, headsetId;

        public EventBuffer<MentalCommand> MentalCommandReceived = new EventBuffer<MentalCommand>();
        public EventBuffer<DeviceInfo> DevDataReceived = new EventBuffer<DeviceInfo>();
        public EventBuffer<SystemEventArgs> SysEventReceived = new EventBuffer<SystemEventArgs>();

        public DataStreamEventBuffer(DataStream stream, string sessionId, string headsetId)
        {
            dataStream = stream;
            this.sessionId = sessionId;
            this.headsetId = headsetId;
            dataStream.MentalCommandReceived += MentalCommandReceived.OnParentEvent;
            dataStream.DevDataReceived += DevDataReceived.OnParentEvent;
            dataStream.SysEventReceived += SysEventReceived.OnParentEvent;
        }

        public void Process()
        {
            MentalCommandReceived.Process();
            DevDataReceived.Process();
            SysEventReceived.Process();
        }
    }
}

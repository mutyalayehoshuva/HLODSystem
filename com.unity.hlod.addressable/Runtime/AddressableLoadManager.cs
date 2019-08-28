using System;
using System.Collections;
using System.Collections.Generic;
using Unity.HLODSystem.Streaming;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Object = UnityEngine.Object;

namespace Unity.HLODSystem
{
    
    public class AddressableLoadManager : MonoBehaviour
    {
        public class Handle : IEnumerator
        {
            public event Action<Handle> Completed;
            public Handle(AddressableController controller, string address, int priority, float distance)
            {
                m_controller = controller;
                m_address = address;
                m_priority = priority;
                m_distance = distance;
            }

            public string Address => m_address;

            public int Priority
            {
                get { return m_priority; }
            }

            public float Distance
            {
                get { return m_distance; }
            }

            public AddressableController Controller
            {
                get { return m_controller; }
            }
            
            public AsyncOperationStatus Status
            {
                get
                {
                    if (m_startLoad == false)
                    {
                        return AsyncOperationStatus.None;
                    }
                    return m_asyncHandle.Status;
                }
            }

            public Object Result
            {
                get { return m_asyncHandle.Result; }
            }
            public bool MoveNext()
            {
                if (m_startLoad == false)
                    return true;
                return !m_asyncHandle.IsDone;
            }

            public void Reset()
            {
            }

            public object Current
            {
                get
                {
                    if (m_startLoad == false)
                        return null;
                    return m_asyncHandle.Result;
                }
            }

            public void Start()
            {
                m_startLoad = true;
                m_asyncHandle = Addressables.LoadAssetAsync<Object>(m_address);
                m_asyncHandle.Completed += handle =>
                {
                    Completed?.Invoke(this);
                };
            }

            public void Stop()
            {
                if (m_startLoad == true)
                {
                    Addressables.Release(m_asyncHandle);
                }
            }


            private AddressableController m_controller;
            private string m_address;
            private int m_priority;
            private float m_distance;
            private bool m_startLoad = false;

            private AsyncOperationHandle<Object> m_asyncHandle;
        }
        #region Singleton
        private static AddressableLoadManager s_instance;
        public static AddressableLoadManager Instance
        {
            get
            {
                if (s_instance == null)
                {
                    GameObject go = new GameObject("AddressableLoadManager");
                    s_instance = go.AddComponent<AddressableLoadManager>();
                    DontDestroyOnLoad(go);
                }

                return s_instance;
            }
        }
        #endregion


        private Handle m_currentHandle = null;
        private LinkedList<Handle> m_loadQueue = new LinkedList<Handle>();
        public void RegisterController(AddressableController controller)
        {
        }

        public void UnregisterController(AddressableController controller)
        {
            var node = m_loadQueue.First;
            while (node != null)
            {
                if (node.Value.Controller == controller)
                {
                    var remove = node;
                    node = node.Next;
                    m_loadQueue.Remove(remove);
                }
                else
                {
                    node = node.Next;
                }
            }

        }

        IEnumerator Start()
        {
            while (true)
            {
                while (m_loadQueue.First == null)
                    yield return null;

                m_currentHandle = m_loadQueue.First.Value;
                m_loadQueue.RemoveFirst();

                m_currentHandle.Start();
                
                Debug.Log($"LoadingAsset: {m_currentHandle.Priority}, {m_currentHandle.Distance}");
                
                while (m_currentHandle != null && m_currentHandle.MoveNext())
                {
                    yield return null;
                }
            }
        }

        public Handle LoadAsset(AddressableController controller, string address, int priority, float distance)
        {
            Debug.Log($"LoadAsset: {address}, {priority}, {distance}");
            Handle handle = new Handle(controller, address, priority, distance);
            InsertHandle(handle);
            return handle;
        }

        public void UnloadAsset(Handle handle)
        {
            Debug.Log($"UnloadAsset: {handle.Address}, {handle.Priority}, {handle.Distance}");
            
            m_loadQueue.Remove(handle);
            handle.Stop();
            if (m_currentHandle == handle)
                m_currentHandle = null;
        }


        private void InsertHandle(Handle handle)
        {
            
            var node = m_loadQueue.First;
            while (node != null && node.Value.Priority < handle.Priority)
            {
                node = node.Next;
            }

            while (node != null && node.Value.Priority == handle.Priority && node.Value.Distance < handle.Distance)
            {
                node = node.Next;
            }

            if (node == null)
                m_loadQueue.AddLast(handle);
            else
                m_loadQueue.AddBefore(node, handle);
            //m_loadQueue.AddLast(handle);
        }
   
    }
}
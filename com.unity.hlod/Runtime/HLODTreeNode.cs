﻿using System;
using System.Collections;
using System.Collections.Generic;
using Unity.HLODSystem.SpaceManager;
using Unity.HLODSystem.Streaming;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Unity.HLODSystem
{
    [Serializable]
    public class HLODTreeNode
    {
        [SerializeField] 
        private int m_level;
        [SerializeField]
        private Bounds m_bounds;
        [SerializeField]
        private List<HLODTreeNode> m_childTreeNodes;

        [SerializeField]
        private List<int> m_highObjectIds = new List<int>();
        [SerializeField]
        private List<int> m_lowObjectIds = new List<int>();

        private Dictionary<int, GameObject> m_highObjects = new Dictionary<int, GameObject>();
        private Dictionary<int, GameObject> m_lowObjects = new Dictionary<int, GameObject>();

        private Dictionary<int, GameObject> m_loadedHighObjects;
        private Dictionary<int, GameObject> m_loadedLowObjects;

        public int Level
        {
            set { m_level = value; }
            get { return m_level; }
        }
        public Bounds Bounds
        {
            set { m_bounds = value; }
            get { return m_bounds; }
        }

        public List<HLODTreeNode> ChildNodes
        {
            set { m_childTreeNodes = value;}
            get { return m_childTreeNodes; }
        }

        public List<int> HighObjectIds
        {
            get { return m_highObjectIds; }
        }

        public List<int> LowObjectIds
        {
            get { return m_lowObjectIds; }
        }

        enum State
        {
            Release,
            Low,
            High,
        }

        private FSM<State> m_fsm = new FSM<State>();
        private State m_lastState = State.Release;

        private ControllerBase m_controller;
        private ISpaceManager m_spaceManager;
        private HLODTreeNode m_parent;

        private float m_boundsLength;
        private float m_distance;
        
        private bool m_isVisible;
        private bool m_isVisibleHierarchy;


        public void Initialize(ControllerBase controller, ISpaceManager spaceManager, HLODTreeNode parent)
        {
            for (int i = 0; i < m_childTreeNodes.Count; ++i)
            {
                m_childTreeNodes[i].Initialize(controller, spaceManager, this);
            }

            //set to initialize state
            m_fsm.ChangeState(State.Release);

            m_fsm.RegisterEnteringFunction(State.Release, OnEnteringRelease);
            m_fsm.RegisterEnteredFunction(State.Release, OnEnteredRelease);

            m_fsm.RegisterEnteringFunction(State.Low, OnEnteringLow);
            m_fsm.RegisterEnteredFunction(State.Low, OnEnteredLow);
            m_fsm.RegisterExitedFunction(State.Low, OnExitedLow);

            m_fsm.RegisterEnteringFunction(State.High, OnEnteringHigh);
            m_fsm.RegisterEnteredFunction(State.High, OnEnteredHigh);
            m_fsm.RegisterExitedFunction(State.High, OnExitedHigh);
            
            m_controller = controller;
            m_spaceManager = spaceManager;
            m_parent = parent;
            
            m_isVisible = true;
            m_isVisibleHierarchy = true;

            m_boundsLength = m_bounds.extents.x * m_bounds.extents.x + m_bounds.extents.z * m_bounds.extents.z;
        }

        public void Cull(bool isCull)
        {
            if (isCull)
            {
                Release();
            }
            else
            {
                if (m_fsm.LastState == State.Release)
                {
                    m_fsm.ChangeState(State.Low);
                }
            }
        }

        #region FSM functions

        IEnumerator OnEnteringRelease()
        {
            if ( m_parent == null )
                yield break;

            while (m_parent.m_fsm.CurrentState == State.High)
                yield return null;
        }
        void OnEnteredRelease()
        {
            for (int i = 0; i < m_childTreeNodes.Count; ++i)
            {
                m_childTreeNodes[i].m_isVisible = false;
                m_childTreeNodes[i].Release();
            }
     
        }
        
        IEnumerator OnEnteringLow()
        {
            if (m_lowObjects.Count == m_lowObjectIds.Count)
                yield break;
             
            if ( m_loadedLowObjects == null ) 
                m_loadedLowObjects = new Dictionary<int, GameObject>();
                         
            for (int i = 0; i < m_lowObjectIds.Count; ++i)
            {
                int id = m_lowObjectIds[i];
             
                var loadHandle = m_controller.GetLowObject(id, Level, m_distance);
                yield return loadHandle;
                             
                loadHandle.Result.SetActive(false);
                m_loadedLowObjects.Add(id, loadHandle.Result);

            }
        }
        
        void OnEnteredLow()
        {
            m_lowObjects = m_loadedLowObjects;
            m_loadedLowObjects = null;

            for (int i = 0; i < m_childTreeNodes.Count; ++i)
            {
                m_childTreeNodes[i].Release();
            }
         
        }

        void OnExitedLow()
        {
            foreach (var item in m_lowObjects)
            {
                item.Value.SetActive(false);
                m_controller.ReleaseLowObject(item.Key);
            }
            m_lowObjects.Clear();
        }

        IEnumerator OnEnteringHigh()
        {
            //child low mesh should be load before change to high.
            for (int i = 0; i < m_childTreeNodes.Count; ++i)
            {                
                m_childTreeNodes[i].m_isVisible = false;
                m_childTreeNodes[i].m_fsm.ChangeState(State.Low);
            }

            if ( m_loadedHighObjects == null )
                m_loadedHighObjects = new Dictionary<int, GameObject>();
            
            for (int i = 0; i < m_highObjectIds.Count; ++i)
            {
                int id = m_highObjectIds[i];


                var loadHandle = m_controller.GetHighObject(id, Level, m_distance);
                yield return loadHandle;
                                
                loadHandle.Result.SetActive(false);
                m_loadedHighObjects.Add(id, loadHandle.Result);

            }

            for (int i = 0; i < m_childTreeNodes.Count; ++i)
            {
                while (m_childTreeNodes[i].m_fsm.CurrentState == State.Release)
                    yield return null;
            }
        }

        void OnEnteredHigh()
        {
            for (int i = 0; i < m_childTreeNodes.Count; ++i)
            {
                m_childTreeNodes[i].m_isVisible = true;
            }

            m_highObjects = m_loadedHighObjects;
            m_loadedHighObjects = null;
        }

        void OnExitedHigh()
        {
            foreach (var item in m_highObjects)
            {
                item.Value.SetActive(false);
                m_controller.ReleaseHighObject(item.Key);
            }
            m_highObjects.Clear();
            
            for (int i = 0; i < m_childTreeNodes.Count; ++i)
            {
                m_childTreeNodes[i].Release();
                m_childTreeNodes[i].m_isVisible = false;
            }
        }


        void Release()
        {
            m_fsm.ChangeState(State.Release);
        }
        #endregion
        

        public void Update(float lodDistance)
        {
            if (m_fsm.LastState!= State.Release)
            {
                if (m_spaceManager.IsHigh(lodDistance, m_bounds))
                {
                    if ( m_fsm.CurrentState == State.Low &&
                         m_isVisible == true)       //< if isVisible is false, it loaded from parent but not showing. 
                                                    //< We have to wait for showing after then, change state to high.
                        m_fsm.ChangeState(State.High);
                }
                else
                {
                    m_fsm.ChangeState(State.Low);
                }
            }

            m_distance = m_spaceManager.GetDistanceSqure(m_bounds) - m_boundsLength;

            m_fsm.Update();
            UpdateVisible();
            
            for (int i = 0; i < m_childTreeNodes.Count; ++i)
            {
                m_childTreeNodes[i].Update(lodDistance);
            }
        }

        private void UpdateVisible()
        {
            if (m_parent != null)
            {
                m_isVisibleHierarchy = m_isVisible && m_parent.m_isVisibleHierarchy;
            }
            else
            {
                m_isVisibleHierarchy = m_isVisible;    
            }

            foreach (var item in m_highObjects)
            {
                item.Value.SetActive(m_isVisibleHierarchy);
            }

            foreach (var item in m_lowObjects)
            {
                item.Value.SetActive(m_isVisibleHierarchy);
            }
        }

    }

}
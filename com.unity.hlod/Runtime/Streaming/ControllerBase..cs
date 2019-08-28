﻿using System;
using System.Collections;
using System.Collections.Generic;
using Unity.HLODSystem.SpaceManager;
using UnityEngine;


namespace Unity.HLODSystem.Streaming
{
    using ControllerID = Int32;
    public abstract class ControllerBase : MonoBehaviour
    {
        public interface ILoadHandle : IEnumerator
        {
            GameObject Result { get; }
        }
        #region Interface
        public abstract void Install();


        public abstract void OnStart();
        public abstract void OnStop();

        //This should be a coroutine.
        public abstract ILoadHandle GetHighObject(ControllerID id, int level, float distance);

        public abstract ILoadHandle GetLowObject(ControllerID id, int level, float distance);

        public abstract void ReleaseHighObject(ControllerID id);
        public abstract void ReleaseLowObject(ControllerID id);
        #endregion

        #region Unity Events
        void Awake()
        {
            m_spaceManager = new QuadTreeSpaceManager();
        }

        void Start()
        {
            m_root.Initialize(this, m_spaceManager, null);
            OnStart();
        }

        void OnEnable()
        {
            HLODManager.Instance.Register(this);
        }

        void OnDisable()
        {
            HLODManager.Instance.Unregister(this);
        }

        void OnDestroy()
        {
            OnStop();
            HLODManager.Instance.Unregister(this);
            m_spaceManager = null;
            m_root = null;
        }
        #endregion
        
        #region Method
        public void UpdateCull(Camera camera)
        {
            if (m_spaceManager == null)
                return;

            m_spaceManager.UpdateCamera(this.transform, camera);

            m_root.Cull(m_spaceManager.IsCull(m_cullDistance, m_root.Bounds));
            m_root.Update(m_lodDistance);
            
         
        }
        #endregion
 
        #region variables
        private ISpaceManager m_spaceManager;
        
        [SerializeField]
        private HLODTreeNode m_root;

        [SerializeField] private float m_cullDistance;
        [SerializeField] private float m_lodDistance;
        
        public HLODTreeNode Root
        {
            set { m_root = value; }
            get { return m_root; }
        }

        public float CullDistance
        {
            set { m_cullDistance = value; }
            get { return m_cullDistance; }
        }

        public float LODDistance
        {
            set { m_lodDistance = value; }
            get { return m_lodDistance; }
        }
        #endregion
    }

}
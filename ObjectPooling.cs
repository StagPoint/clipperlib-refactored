// Copyright (c) 2017 StagPoint Software

namespace ClipperLib
{
	using System;
	using System.Collections.Generic;

	/// <summary>
	/// Defines the minimum interface an object must implement in order to be used with the 
	/// LocalObjectPool class.
	/// </summary>
	internal interface IPooledObject
	{
		void PrepareForRecycle();
	}

	/// <summary>
	/// If a pooled object must be initialized after being recycled, it should implement this 
	/// interface.
	/// </summary>
	internal interface IRecycledObjectInit
	{
		void OnObjectRecycled();
	}

	/// <summary>
	/// Implements an object pool that tracks every instance that is served, and allows all of those
	/// instances to be recycled at one time. This is particularly useful when integrating object
	/// pooling into an unfamiliar and complex third-party codebase, where it can be difficult to 
	/// track every place where an object instance goes out of scope or is no longer needed, but 
	/// there is a clear point at which none of the objects are needed any longer.
	/// </summary>
	/// <typeparam name="T"></typeparam>
	internal abstract class LocalObjectPool<T> where T : class, IPooledObject
	{
		#region Public properties 

		/// <summary>
		/// Returns the total number of objects created over the lifetime of this object pool
		/// </summary>
		public int NumberOfObjectsCreated
		{
			get { return m_objectsInstantiated; }
		}

		/// <summary>
		/// Returns the number of objects currently available from the object pool
		/// </summary>
		public int NumberOfObjectsInPool
		{
			get
			{
				lock( m_syncLock )
				{
					return m_objectPool.Count;
				}
			}
		}

		#endregion 

		#region Private fields

		private object m_syncLock = new object();

		private Stack<T> m_objectPool = new Stack<T>();
		private List<T> m_activeObjects = new List<T>();

		private int m_objectsInstantiated = 0;

		#endregion 

		#region Public functions 

		protected abstract T AllocateNewInstance();

		public T ClaimFromPool()
		{
			lock( m_syncLock )
			{
				T instance = null;
				if( m_objectPool.Count > 0 )
				{
					instance = m_objectPool.Pop();
				}
				else
				{
					instance = AllocateNewInstance();
					m_objectsInstantiated += 1;
				}

				m_activeObjects.Add( instance );

				if( instance is IRecycledObjectInit )
				{
					( (IRecycledObjectInit)instance ).OnObjectRecycled();
				}

				return instance;
			}
		}

		public void ReturnAllToPool()
		{
			lock( m_syncLock )
			{
				for( int i = 0; i < m_activeObjects.Count; i++ )
				{
					m_activeObjects[ i ].PrepareForRecycle();
					m_objectPool.Push( m_activeObjects[ i ] );
				}

				m_activeObjects.Clear();
			}
		}

		public void ReleaseObjectPool()
		{
			lock( m_syncLock )
			{
				m_objectPool.Clear();
				m_objectPool.TrimExcess();

				m_activeObjects.Clear();
				m_activeObjects.TrimExcess();
			}
		}

		#endregion 

		#region System.Object overrides 

		public override string ToString()
		{
			return string.Format( "Created: {0}, Available: {1}", this.NumberOfObjectsCreated, this.NumberOfObjectsInPool );
		}

		#endregion 
	}
}
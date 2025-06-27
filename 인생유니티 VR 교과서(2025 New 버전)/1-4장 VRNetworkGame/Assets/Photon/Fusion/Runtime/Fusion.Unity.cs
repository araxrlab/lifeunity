#if !FUSION_DEV

#region Assets/Photon/Fusion/Runtime/FusionAssetSource.Common.cs

// merged AssetSource

#region NetworkAssetSourceAddressable.cs

#if (FUSION_ADDRESSABLES || FUSION_ENABLE_ADDRESSABLES) && !FUSION_DISABLE_ADDRESSABLES
namespace Fusion {
  using System;
  using UnityEngine;
  using UnityEngine.AddressableAssets;
  using UnityEngine.ResourceManagement.AsyncOperations;
  using static InternalLogStreams;

  /// <summary>
  /// An Addressables-based implementation of the asset source pattern. The asset is loaded from the Addressables system.
  /// </summary>
  /// <typeparam name="T"></typeparam>
  [Serializable]
  public partial class NetworkAssetSourceAddressable<T> where T : UnityEngine.Object {
    
    /// <see cref="RuntimeKey"/>
    [Obsolete("Use RuntimeKey instead")]
    public AssetReference Address {
      get {
        if (string.IsNullOrEmpty(RuntimeKey)) {
          return default;
        }
        return FusionAddressablesUtils.CreateAssetReference(RuntimeKey);
      }
      set {
        if (value.IsValid()) {
          RuntimeKey = (string)value.RuntimeKey;
        } else {
          RuntimeKey = string.Empty;
        }
      }
    }
    
    /// <summary>
    /// Addressables runtime key. Can be used in any form Addressables supports, such as asset name, label, or address.
    /// </summary>
    [UnityAddressablesRuntimeKey]
    public string RuntimeKey;
    
    [NonSerialized]
    private int _acquireCount;

    [NonSerialized] 
    private AsyncOperationHandle _op;

    /// <inheritdoc cref="NetworkAssetSourceResource{T}.Acquire"/>
    public void Acquire(bool synchronous) {
      if (_acquireCount == 0) {
        LoadInternal(synchronous);
      }
      _acquireCount++;
    }

    /// <inheritdoc cref="NetworkAssetSourceResource{T}.Release"/>
    public void Release() {
      if (_acquireCount <= 0) {
        throw new Exception("Asset is not loaded");
      }
      if (--_acquireCount == 0) {
        UnloadInternal();
      }
    }

    /// <inheritdoc cref="NetworkAssetSourceResource{T}.IsCompleted"/>
    public bool IsCompleted => _op.IsDone;

    /// <inheritdoc cref="NetworkAssetSourceResource{T}.WaitForResult"/>
    public T WaitForResult() {
      Assert.Check(_op.IsValid());
      if (!_op.IsDone) {
        try {
          _op.WaitForCompletion();
        } catch (Exception e) when (!Application.isPlaying && typeof(Exception) == e.GetType()) {
          LogError?.Log($"An exception was thrown when loading asset: {RuntimeKey}; since this method " +
                        $"was called from the editor, it may be due to the fact that Addressables don't have edit-time load support. Please use EditorInstance instead.");
          throw;
        }
      }
      
      if (_op.OperationException != null) {
        throw new InvalidOperationException($"Failed to load asset: {RuntimeKey}", _op.OperationException);
      }
      
      Assert.Check(_op.Result != null, "_op.Result != null");
      return ValidateResult(_op.Result);
    }
    
    private void LoadInternal(bool synchronous) {
      Assert.Check(!_op.IsValid());

      _op = Addressables.LoadAssetAsync<UnityEngine.Object>(RuntimeKey);
      if (!_op.IsValid()) {
        throw new Exception($"Failed to load asset: {RuntimeKey}");
      }
      if (_op.Status == AsyncOperationStatus.Failed) {
        throw new Exception($"Failed to load asset: {RuntimeKey}", _op.OperationException);
      }
      
      if (synchronous) {
        _op.WaitForCompletion();
      }
    }

    private void UnloadInternal() {
      if (_op.IsValid()) {
        var op = _op;
        _op = default;
        Addressables.Release(op);  
      }
    }

    private T ValidateResult(object result) {
      if (result == null) {
        throw new InvalidOperationException($"Failed to load asset: {RuntimeKey}; asset is null");
      }
      if (typeof(T).IsSubclassOf(typeof(Component))) {
        if (result is GameObject gameObject == false) {
          throw new InvalidOperationException($"Failed to load asset: {RuntimeKey}; asset is not a GameObject, but a {result.GetType()}");
        }
        
        var component = ((GameObject)result).GetComponent<T>();
        if (!component) {
          throw new InvalidOperationException($"Failed to load asset: {RuntimeKey}; asset does not contain component {typeof(T)}");
        }

        return component;
      }

      if (result is T asset) {
        return asset;
      }
      
      throw new InvalidOperationException($"Failed to load asset: {RuntimeKey}; asset is not of type {typeof(T)}, but {result.GetType()}");
    }
    
    /// <inheritdoc cref="NetworkAssetSourceResource{T}.Description"/>
    public string Description => "RuntimeKey: " + RuntimeKey;
    
#if UNITY_EDITOR
    /// <inheritdoc cref="NetworkAssetSourceResource{T}.EditorInstance"/>
    public T EditorInstance => (T)FusionAddressablesUtils.LoadEditorInstance(RuntimeKey);
#endif
  }
}
#endif

#endregion


#region NetworkAssetSourceResource.cs

namespace Fusion {
  using System;
  using System.Runtime.ExceptionServices;
  using UnityEngine;
  using Object = UnityEngine.Object;
  using UnityResources = UnityEngine.Resources;

  /// <summary>
  /// Resources-based implementation of the asset source pattern.
  /// </summary>
  /// <typeparam name="T"></typeparam>
  [Serializable]
  public partial class NetworkAssetSourceResource<T> where T : UnityEngine.Object {
    
    /// <summary>
    /// Resource path. Note that this is a Unity resource path, not a file path.
    /// </summary>
    [UnityResourcePath(typeof(Object))]
    public string ResourcePath;
    /// <summary>
    /// Sub-object name. If empty, the main object is loaded.
    /// </summary>
    public string SubObjectName;

    [NonSerialized]
    private object _state;
    [NonSerialized]
    private int    _acquireCount;

    /// <summary>
    /// Loads the asset. In synchronous mode, the asset is loaded immediately. In asynchronous mode, the asset is loaded in the background.
    /// </summary>
    /// <param name="synchronous"></param>
    public void Acquire(bool synchronous) {
      if (_acquireCount == 0) {
        LoadInternal(synchronous);
      }
      _acquireCount++;
    }

    /// <summary>
    /// Unloads the asset. If the asset is not loaded, an exception is thrown. If the asset is loaded multiple times, it is only
    /// unloaded when the last acquire is released.
    /// </summary>
    /// <exception cref="Exception"></exception>
    public void Release() {
      if (_acquireCount <= 0) {
        throw new Exception("Asset is not loaded");
      }
      if (--_acquireCount == 0) {
        UnloadInternal();
      }
    }

    /// <summary>
    /// Returns <see langword="true"/> if the asset is loaded.
    /// </summary>
    public bool IsCompleted {
      get {
        if (_state == null) {
          // hasn't started
          return false;
        }
        
        if (_state is ResourceRequest asyncOp && !asyncOp.isDone) {
          // still loading, wait
          return false;
        }

        return true;
      }
    }

    /// <summary>
    /// Blocks until the asset is loaded. If the asset is not loaded, an exception is thrown.
    /// </summary>
    /// <returns>The loaded asset</returns>
    public T WaitForResult() {
      Assert.Check(_state != null);
      if (_state is ResourceRequest asyncOp) {
        if (asyncOp.isDone) {
          FinishAsyncOp(asyncOp);
        } else {
          // just load synchronously, then pass through
          _state = null;
          LoadInternal(synchronous: true);
        }
      }
      
      if (_state == null) {
        throw new InvalidOperationException($"Failed to load asset {typeof(T)}: {ResourcePath}[{SubObjectName}]. Asset is null.");  
      }

      if (_state is T asset) {
        return asset;
      }

      if (_state is ExceptionDispatchInfo exception) {
        exception.Throw();
        throw new NotSupportedException();
      }

      throw new InvalidOperationException($"Failed to load asset {typeof(T)}: {ResourcePath}, SubObjectName: {SubObjectName}");
    }

    private void FinishAsyncOp(ResourceRequest asyncOp) {
      try {
        var asset = string.IsNullOrEmpty(SubObjectName) ? asyncOp.asset : LoadNamedResource(ResourcePath, SubObjectName);
        if (asset) {
          _state = asset;
        } else {
          throw new InvalidOperationException($"Missing Resource: {ResourcePath}, SubObjectName: {SubObjectName}");
        }
      } catch (Exception ex) {
        _state = ExceptionDispatchInfo.Capture(ex);
      }
    }
    
    private static T LoadNamedResource(string resoucePath, string subObjectName) {
      var assets = UnityResources.LoadAll<T>(resoucePath);

      for (var i = 0; i < assets.Length; ++i) {
        var asset = assets[i];
        if (string.Equals(asset.name, subObjectName, StringComparison.Ordinal)) {
          return asset;
        }
      }

      return null;
    }
    
    private void LoadInternal(bool synchronous) {
      Assert.Check(_state == null);
      try {
        if (synchronous) {
          _state = string.IsNullOrEmpty(SubObjectName) ? UnityResources.Load<T>(ResourcePath) : LoadNamedResource(ResourcePath, SubObjectName);
        } else {
          _state = UnityResources.LoadAsync<T>(ResourcePath);
        }

        if (_state == null) {
          _state = new InvalidOperationException($"Missing Resource: {ResourcePath}, SubObjectName: {SubObjectName}");
        }
      } catch (Exception ex) {
        _state = ExceptionDispatchInfo.Capture(ex);
      }
    }

    private void UnloadInternal() {
      if (_state is ResourceRequest asyncOp) {
        asyncOp.completed += op => {
          // unload stuff
        };
      } else if (_state is Object) {
        // unload stuff
      }

      _state = null;
    }
    
    /// <summary>
    /// The description of the asset source. Used for debugging.
    /// </summary>
    public string Description => $"Resource: {ResourcePath}{(!string.IsNullOrEmpty(SubObjectName) ? $"[{SubObjectName}]" : "")}";
    
#if UNITY_EDITOR
    /// <summary>
    /// Returns the asset instance for Editor purposes. Does not call <see cref="Acquire"/>.
    /// </summary>
    public T EditorInstance => string.IsNullOrEmpty(SubObjectName) ? UnityResources.Load<T>(ResourcePath) : LoadNamedResource(ResourcePath, SubObjectName);
#endif
  }
}

#endregion


#region NetworkAssetSourceStatic.cs

namespace Fusion {
  using System;
  using UnityEngine.Serialization;

  /// <summary>
  /// Hard reference-based implementation of the asset source pattern. This asset source forms a hard reference to the asset and never releases it.
  /// This type is meant to be used at runtime. For edit-time, prefer <see cref="NetworkAssetSourceStaticLazy{T}"/>, as it delays
  /// actually loading the asset, improving the editor performance.
  /// </summary>
  /// <typeparam name="T"></typeparam>
  [Serializable]
  public partial class NetworkAssetSourceStatic<T> where T : UnityEngine.Object {

    /// <summary>
    /// The asset reference. Can point to an asset or to a runtime-created object.
    /// </summary>
    [FormerlySerializedAs("Prefab")]
    public T Object;
    
    /// <see cref="Object"/>
    [Obsolete("Use Asset instead")]
    public T Prefab {
      get => Object;
      set => Object = value;
    }
    
    /// <summary>
    /// Returns <see langword="true"/>.
    /// </summary>
    public bool IsCompleted => true;

    /// <summary>
    /// Does nothing, the asset is always loaded.
    /// </summary>
    public void Acquire(bool synchronous) {
      // do nothing
    }

    /// <summary>
    /// Does nothing, the asset is always loaded.
    /// </summary>
    public void Release() {
      // do nothing
    }

    /// <summary>
    /// Returns <seealso cref="Object"/> or throws an exception if the reference is missing.
    /// </summary>
    public T WaitForResult() {
      if (Object == null) {
        throw new InvalidOperationException("Missing static reference");
      }

      return Object;
    }
    
    /// <inheritdoc cref="NetworkAssetSourceResource{T}.Description"/>
    public string Description {
      get {
        if (Object) {
#if UNITY_EDITOR
          if (UnityEditor.AssetDatabase.TryGetGUIDAndLocalFileIdentifier(Object, out var guid, out long fileID)) {
            return $"Static: {guid}, fileID: {fileID}";
          }
#endif
          return "Static: " + Object;
        } else {
          return "Static: (null)";
        }
      }
    }
    
#if UNITY_EDITOR
    /// <summary>
    /// Returns <seealso cref="Object"/>.
    /// </summary>
    public T EditorInstance => Object;
#endif
  }
}

#endregion


#region NetworkAssetSourceStaticLazy.cs

namespace Fusion {
  using System;
  using UnityEngine;
  using UnityEngine.Serialization;

  /// <summary>
  /// An edit-time optimised version of <see cref="NetworkAssetSourceStatic{T}"/>, taking advantage of Unity's lazy loading of
  /// assets. At runtime, this type behaves exactly like <see cref="NetworkAssetSourceStatic{T}"/>, except for the inability
  /// to use runtime-created objects.
  /// </summary>
  /// <typeparam name="T"></typeparam>
  [Serializable]
  public partial class NetworkAssetSourceStaticLazy<T> where T : UnityEngine.Object {
    
    /// <summary>
    /// The asset reference. Can only point to an asset, runtime-created objects will not work.
    /// </summary>
    [FormerlySerializedAs("Prefab")] 
    public LazyLoadReference<T> Object;
    
    /// <inheritdoc cref="NetworkAssetSourceStatic{T}.Prefab"/>
    [Obsolete("Use Object instead")]
    public LazyLoadReference<T> Prefab {
      get => Object;
      set => Object = value;
    }
    
    /// <inheritdoc cref="NetworkAssetSourceStatic{T}.IsCompleted"/>
    public bool IsCompleted => true;
    
    /// <inheritdoc cref="NetworkAssetSourceStatic{T}.Acquire"/>
    public void Acquire(bool synchronous) {
      // do nothing
    }
    
    /// <inheritdoc cref="NetworkAssetSourceStatic{T}.Release"/>
    public void Release() {
      // do nothing
    }
    
    /// <inheritdoc cref="NetworkAssetSourceStatic{T}.WaitForResult"/>
    public T WaitForResult() {
      if (Object.asset == null) {
        throw new InvalidOperationException("Missing static reference");
      }

      return Object.asset;
    }
    
    /// <inheritdoc cref="NetworkAssetSourceStatic{T}.Description"/>
    public string Description {
      get {
        if (Object.isBroken) {
          return "Static: (broken)";
        } else if (Object.isSet) {
#if UNITY_EDITOR
          if (UnityEditor.AssetDatabase.TryGetGUIDAndLocalFileIdentifier(Object.instanceID, out var guid, out long fileID)) {
            return $"Static: {guid}, fileID: {fileID}";
          }
#endif
          return "Static: " + Object.asset;
        } else {
          return "Static: (null)";
        }
      }
    }
    
#if UNITY_EDITOR
    /// <inheritdoc cref="NetworkAssetSourceStatic{T}.EditorInstance"/>
    public T EditorInstance => Object.asset;
#endif
  }
}

#endregion


#region FusionGlobalScriptableObjectAddressAttribute.cs

namespace Fusion {
  using System;
  using UnityEngine.Scripting;
#if (FUSION_ADDRESSABLES || FUSION_ENABLE_ADDRESSABLES) && !FUSION_DISABLE_ADDRESSABLES 
  using UnityEngine.AddressableAssets;
  using UnityEngine.ResourceManagement.AsyncOperations;
#endif
  using static InternalLogStreams;
  
  /// <summary>
  /// If applied at the assembly level, allows <see cref="FusionGlobalScriptableObject{T}"/> to be loaded with Addressables.
  /// </summary>
  [Preserve]
  public class FusionGlobalScriptableObjectAddressAttribute : FusionGlobalScriptableObjectSourceAttribute {
    /// <param name="objectType">The type this attribute will attempt to load.</param>
    /// <param name="address">The address to load from.</param>
    public FusionGlobalScriptableObjectAddressAttribute(Type objectType, string address) : base(objectType) {
      Address = address;
    }

    /// <summary>
    /// The address to load from.
    /// </summary>
    public string Address { get; }
    
    /// <summary>
    /// Loads the asset from the <see cref="Address"/>. Uses WaitForCompletion internally, so platforms that do not support it need
    /// to preload the address prior to loading.
    /// </summary>
    public override FusionGlobalScriptableObjectLoadResult Load(Type type) {
#if (FUSION_ADDRESSABLES || FUSION_ENABLE_ADDRESSABLES) && !FUSION_DISABLE_ADDRESSABLES
      Assert.Check(!string.IsNullOrEmpty(Address));
      
      var op = Addressables.LoadAssetAsync<FusionGlobalScriptableObject>(Address);
      var instance = op.WaitForCompletion();
      if (op.Status == AsyncOperationStatus.Succeeded) {
        Assert.Check(instance);
        return new (instance, x => Addressables.Release(op));
      }
      
      
      LogTrace?.Log($"Failed to load addressable at address {Address} for type {type.FullName}: {op.OperationException}");
      return default;
#else
      LogTrace?.Log($"Addressables are not enabled. Unable to load addressable for {type.FullName}");
      return default;
#endif
    }
  }
}

#endregion


#region FusionGlobalScriptableObjectResourceAttribute.cs

namespace Fusion {
  using System;
  using System.IO;
  using System.Reflection;
  using UnityEngine;
  using UnityEngine.Scripting;
  using Object = UnityEngine.Object;
  using static InternalLogStreams;
  
  /// <summary>
  /// If applied at the assembly level, allows <see cref="FusionGlobalScriptableObject{T}"/> to be loaded with Resources.
  /// There is a default registration for this attribute, which attempts to load the asset from Resources using path from
  /// <see cref="FusionGlobalScriptableObjectAttribute"/>.
  /// </summary>
  [Preserve]
  public class FusionGlobalScriptableObjectResourceAttribute : FusionGlobalScriptableObjectSourceAttribute {
    /// <param name="objectType">The type this attribute will attempt to load.</param>
    /// <param name="resourcePath">Resources path or <see langword="null"/>/empty if path from <see cref="FusionGlobalScriptableObjectAttribute"/>
    /// is to be used.</param>
    public FusionGlobalScriptableObjectResourceAttribute(Type objectType, string resourcePath = "") : base(objectType) {
      ResourcePath = resourcePath;
    }
    
    /// <summary>
    /// Path in Resources.
    /// </summary>
    public string ResourcePath { get; }
    /// <summary>
    /// If loaded in the editor, should the result be instantiated instead of returning the asset itself? The default is <see langword="true"/>. 
    /// </summary>
    public bool InstantiateIfLoadedInEditor { get; set; } = true;
    
    /// <summary>
    /// Loads the asset from Resources synchronously.
    /// </summary>
    public override FusionGlobalScriptableObjectLoadResult Load(Type type) {
      
      var attribute = type.GetCustomAttribute<FusionGlobalScriptableObjectAttribute>();
      Assert.Check(attribute != null);

      string resourcePath;
      if (string.IsNullOrEmpty(ResourcePath)) {
        string defaultAssetPath = attribute.DefaultPath;
        var indexOfResources = defaultAssetPath.LastIndexOf("/Resources/", StringComparison.OrdinalIgnoreCase);
        if (indexOfResources < 0) {
          LogTrace?.Log($"The default path {defaultAssetPath} does not contain a /Resources/ folder. Unable to load resource for {type.FullName}.");
          return default;
        }

        // try to load from resources, maybe?
        resourcePath = defaultAssetPath.Substring(indexOfResources + "/Resources/".Length);

        // drop the extension
        if (Path.HasExtension(resourcePath)) {
          resourcePath = resourcePath.Substring(0, resourcePath.LastIndexOf('.'));
        }
      } else {
        resourcePath = ResourcePath;
      }

      var instance = UnityEngine.Resources.Load(resourcePath, type);
      if (!instance) {
        LogTrace?.Log($"Unable to load resource at path {resourcePath} for type {type.FullName}");
        return default;
      }

      if (InstantiateIfLoadedInEditor && Application.isEditor) {
        var clone = Object.Instantiate(instance);
        return new((FusionGlobalScriptableObject)clone, x => Object.Destroy(clone));
      } else {
        return new((FusionGlobalScriptableObject)instance, x => UnityEngine.Resources.UnloadAsset(instance));  
      }
    }
  }
}

#endregion



#endregion


#region Assets/Photon/Fusion/Runtime/FusionBurstIntegration.cs

// deleted

#endregion


#region Assets/Photon/Fusion/Runtime/FusionCoroutine.cs

﻿
namespace Fusion {
  using UnityEngine;
  using System;
  using System.Collections;
  using System.Runtime.ExceptionServices;

  public sealed class FusionCoroutine : ICoroutine, IDisposable  {
    private readonly IEnumerator             _inner;
    private          Action<IAsyncOperation> _completed;
    private          float                   _progress;
    private          Action                  _activateAsync;

    public FusionCoroutine(IEnumerator inner) {
      _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }
      
    public event Action<IAsyncOperation> Completed
    {
      add {
        _completed += value;
        if (IsDone) {
          value(this);
        }
      }
      remove => _completed -= value;
    }

    public bool                  IsDone { get; private set; }
    public ExceptionDispatchInfo Error  { get; private set; }

    bool IEnumerator.MoveNext() {
      try {
        if (_inner.MoveNext()) {
          return true;
        } else {
          IsDone = true;
          _completed?.Invoke(this);
          return false;
        }
      } catch (Exception e) {
        IsDone = true;
        Error  = ExceptionDispatchInfo.Capture(e);
        _completed?.Invoke(this);
        return false;
      }
    }

    void IEnumerator.Reset() {
      _inner.Reset();
      IsDone = false;
      Error  = null;
    }

    object IEnumerator.Current => _inner.Current;
      
    public void Dispose() {
      if (_inner is IDisposable disposable) {
        disposable.Dispose();
      }
    }
  }
}

#endregion


#region Assets/Photon/Fusion/Runtime/FusionLogInitializer.Partial.cs

﻿namespace Fusion {
  using System.Text;
  using System.Threading;
  using UnityEngine;

  partial class FusionLogInitializer {
    static partial void InitializeUnityLoggerUser(ref FusionUnityLogger logger);
    
    static FusionUnityLogger CreateLogger(bool isDarkMode) {
      return new FusionUnityLogger(System.Threading.Thread.CurrentThread, isDarkMode);
    }
  }

  /// <summary>
  /// Fusion logger implementation for Unity.
  /// </summary>
  public class FusionUnityLogger : FusionUnityLoggerBase {

    /// <summary>
    /// Is true, the active runner's tick will be logged.
    /// </summary>
    public bool LogActiveRunnerTick = false;
    
    /// <inheritdoc/>
    public FusionUnityLogger(Thread mainThread, bool isDarkMode) : base(mainThread, isDarkMode) {
    }
    
    /// <inheritdoc/>
    protected override (string, Object) CreateMessage(in LogContext context) {
      var sb = GetThreadSafeStringBuilder(out var isMainThread);
      Debug.Assert(sb.Length == 0);
      
      var obj = context.Source?.GetUnityObject();
      
      try {
        AppendPrefix(sb, context.Flags, context.Prefix);

        var pos = sb.Length;
        if (obj != null) {
          if (obj is NetworkRunner runner) {
            TryAppendRunnerPrefix(sb, runner);
          } else if (obj is NetworkObject networkObject) {
            TryAppendNetworkObjectPrefix(sb, networkObject);
          } else if (obj is SimulationBehaviour simulationBehaviour) {
            TryAppendSimulationBehaviourPrefix(sb, simulationBehaviour);
          } else {
            AppendNameThreadSafe(sb, obj); 
          }
        }

        if (LogActiveRunnerTick) {
          for (var enumerator = NetworkRunner.GetInstancesEnumerator(); enumerator.MoveNext();) {
            var runner = enumerator.Current;
            if (runner == null || !runner.IsSimulationUpdating) {
              continue;
            }
            sb.Append($"[Tick {(int)runner.Tick}{(runner.IsFirstTick ? "F" : "")}{(runner.Stage == 0 ? "" : $" {runner.Stage}")}] ");
          }
        }
        
        if (sb.Length > pos) {
          sb.Append(": ");
        }
        
        sb.Append(context.Message);
        return (sb.ToString(), isMainThread ? obj : null);
      } finally {
        sb.Clear();
      }
    }
    
    bool TryAppendRunnerPrefix(StringBuilder builder, NetworkRunner runner) {
      if ((object)runner == null) {
        return false;
      }
      if (runner.Config?.PeerMode != NetworkProjectConfig.PeerModes.Multiple) {
        return false;
      }

      AppendNameThreadSafe(builder, runner);

      var localPlayer = runner.LocalPlayer;
      if (localPlayer.IsRealPlayer) {
        builder.Append("[P").Append(localPlayer.PlayerId).Append("]");
      } else {
        builder.Append("[P-]");
      }
      
      return true;
    }
    
    bool TryAppendNetworkObjectPrefix(StringBuilder builder, NetworkObject networkObject) {
      if ((object)networkObject == null) {
        return false;
      }

      AppendNameThreadSafe(builder, networkObject);
      
      if (networkObject.Id.IsValid) {
        builder.Append(" ");
        builder.Append(networkObject.Id.ToString());
      }
      
      int pos = builder.Length;
      if (TryAppendRunnerPrefix(builder, networkObject.Runner)) {
        builder.Insert(pos, '@');
      }

      return true;
    }
    
    bool TryAppendSimulationBehaviourPrefix(StringBuilder builder, SimulationBehaviour simulationBehaviour) {
      if ((object)simulationBehaviour == null) {
        return false;
      }

      AppendNameThreadSafe(builder, simulationBehaviour);
      
      if (simulationBehaviour is NetworkBehaviour nb && nb.Id.IsValid) {
        builder.Append(" ");
        builder.Append(nb.Id.ToString());
      }
      
      int pos = builder.Length;
      if (TryAppendRunnerPrefix(builder, simulationBehaviour.Runner)) {
        builder.Insert(pos, '@');
      }

      return true;
    }
  }
}

#endregion


#region Assets/Photon/Fusion/Runtime/FusionProfiler.cs

namespace Fusion {
#if FUSION_PROFILER_INTEGRATION
  using Unity.Profiling;
  using UnityEngine;

  public static class FusionProfiler {
    [RuntimeInitializeOnLoadMethod]
    static void Init() {
      Fusion.EngineProfiler.InterpolationOffsetCallback = f => InterpolationOffset.Sample(f);
      Fusion.EngineProfiler.InterpolationTimeScaleCallback = f => InterpolationTimeScale.Sample(f);
      Fusion.EngineProfiler.InterpolationMultiplierCallback = f => InterpolationMultiplier.Sample(f);
      Fusion.EngineProfiler.InterpolationUncertaintyCallback = f => InterpolationUncertainty.Sample(f);

      Fusion.EngineProfiler.ResimulationsCallback = i => Resimulations.Sample(i);
      Fusion.EngineProfiler.WorldSnapshotSizeCallback = i => WorldSnapshotSize.Sample(i);

      Fusion.EngineProfiler.RoundTripTimeCallback = f => RoundTripTime.Sample(f);

      Fusion.EngineProfiler.InputSizeCallback = i => InputSize.Sample(i);
      Fusion.EngineProfiler.InputQueueCallback = i => InputQueue.Sample(i);

      Fusion.EngineProfiler.RpcInCallback = i => RpcIn.Value += i;
      Fusion.EngineProfiler.RpcOutCallback = i => RpcOut.Value += i;

      Fusion.EngineProfiler.SimualtionTimeScaleCallback = f => SimulationTimeScale.Sample(f);

      Fusion.EngineProfiler.InputOffsetCallback = f => InputOffset.Sample(f);
      Fusion.EngineProfiler.InputOffsetDeviationCallback = f => InputOffsetDeviation.Sample(f);

      Fusion.EngineProfiler.InputRecvDeltaCallback = f => InputRecvDelta.Sample(f);
      Fusion.EngineProfiler.InputRecvDeltaDeviationCallback = f => InputRecvDeltaDeviation.Sample(f);
    }

    public static readonly ProfilerCategory Category = ProfilerCategory.Scripts;

    public static readonly ProfilerCounter<float> InterpolationOffset = new ProfilerCounter<float>(Category, "Interp Offset", ProfilerMarkerDataUnit.Count);
    public static readonly ProfilerCounter<float> InterpolationTimeScale = new ProfilerCounter<float>(Category, "Interp Time Scale", ProfilerMarkerDataUnit.Count);
    public static readonly ProfilerCounter<float> InterpolationMultiplier = new ProfilerCounter<float>(Category, "Interp Multiplier", ProfilerMarkerDataUnit.Count);
    public static readonly ProfilerCounter<float> InterpolationUncertainty = new ProfilerCounter<float>(Category, "Interp Uncertainty", ProfilerMarkerDataUnit.Undefined);

    public static readonly ProfilerCounter<int> InputSize = new ProfilerCounter<int>(Category, "Client Input Size", ProfilerMarkerDataUnit.Bytes);
    public static readonly ProfilerCounter<int> InputQueue = new ProfilerCounter<int>(Category, "Client Input Queue", ProfilerMarkerDataUnit.Count);

    public static readonly ProfilerCounter<int> WorldSnapshotSize = new ProfilerCounter<int>(Category, "Client Snapshot Size", ProfilerMarkerDataUnit.Bytes);
    public static readonly ProfilerCounter<int> Resimulations = new ProfilerCounter<int>(Category, "Client Resims", ProfilerMarkerDataUnit.Count);
    public static readonly ProfilerCounter<float> RoundTripTime = new ProfilerCounter<float>(Category, "Client RTT", ProfilerMarkerDataUnit.Count);

    public static readonly ProfilerCounterValue<int> RpcIn = new ProfilerCounterValue<int>(Category, "RPCs In", ProfilerMarkerDataUnit.Count, ProfilerCounterOptions.FlushOnEndOfFrame | ProfilerCounterOptions.ResetToZeroOnFlush);
    public static readonly ProfilerCounterValue<int> RpcOut = new ProfilerCounterValue<int>(Category, "RPCs Out", ProfilerMarkerDataUnit.Count, ProfilerCounterOptions.FlushOnEndOfFrame | ProfilerCounterOptions.ResetToZeroOnFlush);

    public static readonly ProfilerCounter<float> SimulationTimeScale = new ProfilerCounter<float>(Category, "Simulation Time Scale", ProfilerMarkerDataUnit.Count);

    public static readonly ProfilerCounter<float> InputOffset = new ProfilerCounter<float>(Category, "Input Offset", ProfilerMarkerDataUnit.Count);
    public static readonly ProfilerCounter<float> InputOffsetDeviation = new ProfilerCounter<float>(Category, "Input Offset Dev", ProfilerMarkerDataUnit.Count);

    public static readonly ProfilerCounter<float> InputRecvDelta = new ProfilerCounter<float>(Category, "Input Recv Delta", ProfilerMarkerDataUnit.Count);
    public static readonly ProfilerCounter<float> InputRecvDeltaDeviation = new ProfilerCounter<float>(Category, "Input Recv Delta Dev", ProfilerMarkerDataUnit.Count);
  }
#endif
}

#endregion


#region Assets/Photon/Fusion/Runtime/FusionRuntimeCheck.cs

namespace Fusion {
  using UnityEngine;

  static class FusionRuntimeCheck {

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void RuntimeCheck() {
      RuntimeUnityFlagsSetup.Check_ENABLE_IL2CPP();
      RuntimeUnityFlagsSetup.Check_ENABLE_MONO();

      RuntimeUnityFlagsSetup.Check_UNITY_EDITOR();
      RuntimeUnityFlagsSetup.Check_UNITY_GAMECORE();
      RuntimeUnityFlagsSetup.Check_UNITY_SWITCH();
      RuntimeUnityFlagsSetup.Check_UNITY_WEBGL();
      RuntimeUnityFlagsSetup.Check_UNITY_XBOXONE();

      RuntimeUnityFlagsSetup.Check_NETFX_CORE();
      RuntimeUnityFlagsSetup.Check_NET_4_6();
      RuntimeUnityFlagsSetup.Check_NET_STANDARD_2_0();

      RuntimeUnityFlagsSetup.Check_UNITY_2019_4_OR_NEWER();
    }
  }
}


#endregion


#region Assets/Photon/Fusion/Runtime/FusionTraceChannelsExtensions.cs



namespace Fusion {
  static class TraceChannelsExtensions {
    public static TraceChannels AddChannelsFromDefines(this TraceChannels traceChannels) {
#if FUSION_TRACE_GLOBAL
      traceChannels |= TraceChannels.Global;
#endif
#if FUSION_TRACE_STUN
      traceChannels |= TraceChannels.Stun;
#endif
#if FUSION_TRACE_OBJECT
      traceChannels |= TraceChannels.Object;
#endif
#if FUSION_TRACE_NETWORK
      traceChannels |= TraceChannels.Network;
#endif
#if FUSION_TRACE_PREFAB
      traceChannels |= TraceChannels.Prefab;
#endif
#if FUSION_TRACE_SCENEINFO
      traceChannels |= TraceChannels.SceneInfo;
#endif
#if FUSION_TRACE_SCENEMANAGER
      traceChannels |= TraceChannels.SceneManager;
#endif
#if FUSION_TRACE_SIMULATIONMESSAGE
      traceChannels |= TraceChannels.SimulationMessage;
#endif
#if FUSION_TRACE_HOSTMIGRATION
      traceChannels |= TraceChannels.HostMigration;
#endif
#if FUSION_TRACE_ENCRYPTION
      traceChannels |= TraceChannels.Encryption;
#endif
#if FUSION_TRACE_DUMMYTRAFFIC
      traceChannels |= TraceChannels.DummyTraffic;
#endif
#if FUSION_TRACE_REALTIME
      traceChannels |= TraceChannels.Realtime;
#endif
#if FUSION_TRACE_MEMORYTRACK
      traceChannels |= TraceChannels.MemoryTrack;
#endif
#if FUSION_TRACE_SNAPSHOTS
      traceChannels |= TraceChannels.Snapshots;
#endif
#if FUSION_TRACE_TIME
      traceChannels |= TraceChannels.Time;
#endif
      return traceChannels;
    }
  }
}


#endregion


#region Assets/Photon/Fusion/Runtime/FusionUnityUtility.Common.cs

// merged UnityUtility

#region JsonUtilityExtensions.cs

namespace Fusion {
  using System;
  using System.Collections;
  using System.Collections.Generic;
  using System.IO;
  using System.Text;
  using System.Text.RegularExpressions;
  using UnityEngine;

  /// <summary>
  /// Extends capabilities of <see cref="JsonUtility"/> by adding type annotations to the serialized JSON, Unity object reference
  /// handling and integer enquotement.
  /// </summary>
  public static class JsonUtilityExtensions {
    
    /// <see cref="JsonUtilityExtensions.FromJsonWithTypeAnnotation"/>
    public delegate Type TypeResolverDelegate(string typeName);
    /// <see cref="JsonUtilityExtensions.ToJsonWithTypeAnnotation(object,Fusion.JsonUtilityExtensions.InstanceIDHandlerDelegate)"/>
    public delegate string TypeSerializerDelegate(Type type);
    /// <see cref="JsonUtilityExtensions.ToJsonWithTypeAnnotation(object,Fusion.JsonUtilityExtensions.InstanceIDHandlerDelegate)"/>
    public delegate string InstanceIDHandlerDelegate(object context, int value);
    
    private const string TypePropertyName = "$type";

    /// <summary>
    /// Enquotes integers in the JSON string that are at least <paramref name="minDigits"/> long. This is useful for parsers that
    /// interpret large integers as floating point numbers.
    /// </summary>
    /// <param name="json">JSON to process</param>
    /// <param name="minDigits">Digit threshold to perfom the enquoting</param>
    /// <returns><paramref name="json"/> with long integers enquoted.</returns>
    public static string EnquoteIntegers(string json, int minDigits = 8) {
      var result = Regex.Replace(json, $@"(?<="":\s*)(-?[0-9]{{{minDigits},}})(?=[,}}\n\r\s])", "\"$1\"", RegexOptions.Compiled);
      return result;
    }

    /// <summary>
    /// Converts the object to JSON with type annotations.
    /// </summary>
    /// <param name="obj">Object to be serialized.</param>
    /// <param name="instanceIDHandler">Handler for UnityEngine.Object references. If the handler returns an empty string,
    /// the reference is removed from the final result.</param>
    public static string ToJsonWithTypeAnnotation(object obj, InstanceIDHandlerDelegate instanceIDHandler = null) {
      var sb = new StringBuilder(1000);
      using (var writer = new StringWriter(sb)) {
        ToJsonWithTypeAnnotation(obj, writer, instanceIDHandler: instanceIDHandler);
      }
      return sb.ToString();
    }

    /// <summary>
    /// Converts the object/IList to JSON with type annotations.
    /// </summary>
    /// <param name="obj">Object to be serialized.</param>
    /// <param name="writer">The output TextWriter.</param>
    /// <param name="integerEnquoteMinDigits"><see cref="EnquoteIntegers"/></param>
    /// <param name="typeSerializer">Handler for obtaining serialized type names. If <see langword="null"/>, the short assembly
    /// qualified name (namespace + name + assembly name) will be used.</param>
    /// <param name="instanceIDHandler">Handler for UnityEngine.Object references. If the handler returns an empty string,
    /// the reference is removed from the final result.</param>
    public static void ToJsonWithTypeAnnotation(object obj, TextWriter writer, int? integerEnquoteMinDigits = null, TypeSerializerDelegate typeSerializer = null, InstanceIDHandlerDelegate instanceIDHandler = null) {
      if (obj == null) {
        writer.Write("null");
        return;
      }

      if (obj is IList list) {
        writer.Write("[");
        for (var i = 0; i < list.Count; ++i) {
          if (i > 0) {
            writer.Write(",");
          }

          ToJsonInternal(list[i], writer, integerEnquoteMinDigits, typeSerializer, instanceIDHandler);
        }

        writer.Write("]");
      } else {
        ToJsonInternal(obj, writer, integerEnquoteMinDigits, typeSerializer, instanceIDHandler);
      }
    }
    
    
    /// <summary>
    /// Converts JSON with type annotation to an instance of <typeparamref name="T"/>. If the JSON contains type annotations, they need to match
    /// the expected result type. If there are no type annotations, use <paramref name="typeResolver"/> to return the expected type.
    /// </summary>
    /// <param name="json">JSON to be parsed</param>
    /// <param name="typeResolver">Converts type name to a type instance.</param>
    public static T FromJsonWithTypeAnnotation<T>(string json, TypeResolverDelegate typeResolver = null) {
      if (typeof(T).IsArray) {
        var listType = typeof(List<>).MakeGenericType(typeof(T).GetElementType());
        var list = (IList)Activator.CreateInstance(listType);
        FromJsonWithTypeAnnotationInternal(json, typeResolver, list);

        var array = Array.CreateInstance(typeof(T).GetElementType(), list.Count);
        list.CopyTo(array, 0);
        return (T)(object)array;
      }

      if (typeof(T).GetInterface(typeof(IList).FullName) != null) {
        var list = (IList)Activator.CreateInstance(typeof(T));
        FromJsonWithTypeAnnotationInternal(json, typeResolver, list);
        return (T)list;
      }

      return (T)FromJsonWithTypeAnnotationInternal(json, typeResolver);
    }

    /// <summary>
    /// Converts JSON with type annotation. If there are no type annotations, use <paramref name="typeResolver"/> to return the expected type.
    /// </summary>
    /// <param name="json">JSON to be parsed</param>
    /// <param name="typeResolver">Converts type name to a type instance.</param>
    public static object FromJsonWithTypeAnnotation(string json, TypeResolverDelegate typeResolver = null) {
      Assert.Check(json != null);

      var i = SkipWhiteOrThrow(0);
      if (json[i] == '[') {
        var list = new List<object>();

        // list
        ++i;
        for (var expectComma = false;; expectComma = true) {
          i = SkipWhiteOrThrow(i);

          if (json[i] == ']') {
            break;
          }

          if (expectComma) {
            if (json[i] != ',') {
              throw new InvalidOperationException($"Malformed at {i}: expected ,");
            }
            i = SkipWhiteOrThrow(i + 1);
          }

          var item = FromJsonWithTypeAnnotationToObject(ref i, json, typeResolver);
          list.Add(item);
        }

        return list.ToArray();
      }

      return FromJsonWithTypeAnnotationToObject(ref i, json, typeResolver);

      int SkipWhiteOrThrow(int i) {
        while (i < json.Length && char.IsWhiteSpace(json[i])) {
          i++;
        }

        if (i == json.Length) {
          throw new InvalidOperationException($"Malformed at {i}: expected more");
        }

        return i;
      }
    }

    
    private static object FromJsonWithTypeAnnotationInternal(string json, TypeResolverDelegate typeResolver = null, IList targetList = null) {
      Assert.Check(json != null);

      var i = SkipWhiteOrThrow(0);
      if (json[i] == '[') {
        var list = targetList ?? new List<object>();

        // list
        ++i;
        for (var expectComma = false;; expectComma = true) {
          i = SkipWhiteOrThrow(i);

          if (json[i] == ']') {
            break;
          }

          if (expectComma) {
            if (json[i] != ',') {
              throw new InvalidOperationException($"Malformed at {i}: expected ,");
            }

            i = SkipWhiteOrThrow(i + 1);
          }

          var item = FromJsonWithTypeAnnotationToObject(ref i, json, typeResolver);
          list.Add(item);
        }

        return targetList ?? ((List<object>)list).ToArray();
      }

      if (targetList != null) {
        throw new InvalidOperationException($"Expected list, got {json[i]}");
      }

      return FromJsonWithTypeAnnotationToObject(ref i, json, typeResolver);

      int SkipWhiteOrThrow(int i) {
        while (i < json.Length && char.IsWhiteSpace(json[i])) {
          i++;
        }

        if (i == json.Length) {
          throw new InvalidOperationException($"Malformed at {i}: expected more");
        }

        return i;
      }
    }

    private static void ToJsonInternal(object obj, TextWriter writer, 
      int? integerEnquoteMinDigits = null,
      TypeSerializerDelegate typeResolver = null,
      InstanceIDHandlerDelegate instanceIDHandler = null) {
      Assert.Check(obj != null);
      Assert.Check(writer != null);

      var json = JsonUtility.ToJson(obj);
      if (integerEnquoteMinDigits.HasValue) {
        json = EnquoteIntegers(json, integerEnquoteMinDigits.Value);
      }
      
      var type = obj.GetType();

      writer.Write("{\"");
      writer.Write(TypePropertyName);
      writer.Write("\":\"");

      writer.Write(typeResolver?.Invoke(type) ?? SerializableType.GetShortAssemblyQualifiedName(type));

      writer.Write('\"');

      if (json == "{}") {
        writer.Write("}");
      } else {
        Assert.Check('{' == json[0]);
        Assert.Check('}' == json[^1]);
        writer.Write(',');
        
        if (instanceIDHandler != null) {
          int i = 1;
          
          for (;;) {
            const string prefix = "{\"instanceID\":";
            
            var nextInstanceId = json.IndexOf(prefix, i, StringComparison.Ordinal);
            if (nextInstanceId < 0) {
              break;
            }
            
            // parse the number that follows; may be negative
            var start = nextInstanceId + prefix.Length;
            var end = json.IndexOf('}', start);
            var instanceId = int.Parse(json.AsSpan(start, end - start));
            
            // append that part
            writer.Write(json.AsSpan(i, nextInstanceId - i));
            writer.Write(instanceIDHandler(obj, instanceId));
            i = end + 1;
          }
          
          writer.Write(json.AsSpan(i, json.Length - i));
        } else {
          writer.Write(json.AsSpan(1, json.Length - 1));
        }
      }
    }

    private static object FromJsonWithTypeAnnotationToObject(ref int i, string json, TypeResolverDelegate typeResolver) {
      if (json[i] == '{') {
        var endIndex = FindScopeEnd(json, i, '{', '}');
        if (endIndex < 0) {
          throw new InvalidOperationException($"Unable to find end of object's end (starting at {i})");
        }
        
        Assert.Check(endIndex > i);
        Assert.Check(json[endIndex] == '}');

        var part = json.Substring(i, endIndex - i + 1);
        i = endIndex + 1;

        // read the object, only care about the type; there's no way to map dollar-prefixed property to a C# field,
        // so some string replacing is necessary
        var typeInfo = JsonUtility.FromJson<TypeNameWrapper>(part.Replace(TypePropertyName, nameof(TypeNameWrapper.__TypeName), StringComparison.Ordinal));

        Type type;
        if (typeResolver != null) {
          type = typeResolver(typeInfo.__TypeName);
          if (type == null) {
            return null;
          }
        } else {
          Assert.Check(!string.IsNullOrEmpty(typeInfo?.__TypeName));
          type = Type.GetType(typeInfo.__TypeName, true);
        }
        
        if (type.IsSubclassOf(typeof(ScriptableObject))) {
          var instance = ScriptableObject.CreateInstance(type);
          JsonUtility.FromJsonOverwrite(part, instance);
          return instance;
        } else {
          var instance = JsonUtility.FromJson(part, type);
          return instance;
        }
      }

      if (i + 4 < json.Length && json.AsSpan(i, 4).SequenceEqual("null")) {
        // is this null?
        i += 4;
        return null;
      }

      throw new InvalidOperationException($"Malformed at {i}: expected {{ or null");
    }
    
    internal static int FindObjectEnd(string json, int start = 0) {
      return FindScopeEnd(json, start, '{', '}');
    }
    
    private static int FindScopeEnd(string json, int start, char cstart = '{', char cend = '}') {
      var depth = 0;
      
      if (json[start] != cstart) {
        return -1;
      }

      for (var i = start; i < json.Length; i++) {
        if (json[i] == '"') {
          // can't be escaped
          Assert.Check('\\' != json[i - 1]);
          // now skip until the first unescaped quote
          while (i < json.Length) {
            if (json[++i] == '"')
              // are we escaped?
            {
              if (json[i - 1] != '\\') {
                break;
              }
            }
          }
        } else if (json[i] == cstart) {
          depth++;
        } else if (json[i] == cend) {
          depth--;
          if (depth == 0) {
            return i;
          }
        }
      }

      return -1;
    }
    
    [Serializable]
    private class TypeNameWrapper {
#pragma warning disable CS0649 // Set by serialization
      // ReSharper disable once InconsistentNaming
      public string __TypeName;
#pragma warning restore CS0649
    }
  }
}

#endregion


#region FusionAddressablesUtils.cs

#if (FUSION_ADDRESSABLES || FUSION_ENABLE_ADDRESSABLES) && !FUSION_DISABLE_ADDRESSABLES
namespace Fusion {
  using System;
  using UnityEngine.AddressableAssets;
  using Object = UnityEngine.Object;

  /// <summary>
  /// Utility class for addressables.
  /// </summary>
  public static class FusionAddressablesUtils {
    /// <summary>
    /// Tries to parse the address into main part and sub object name.
    /// </summary>
    /// <param name="address">The address to parse.</param>
    /// <param name="mainPart">The main part of the address.</param>
    /// <param name="subObjectName">The sub object name.</param>
    /// <returns><see langword="true"/> if the address is successfully parsed; otherwise, <see langword="false"/>.</returns>
    public static bool TryParseAddress(string address, out string mainPart, out string subObjectName) {
      if (string.IsNullOrEmpty(address)) {
        mainPart = null;
        subObjectName = null;
        return false;
      }

      var indexOfSquareBracket = address.IndexOf('[');
      var indexOfClosingSquareBracket = address.IndexOf(']');

      // addresses can only use square brackets for sub object names
      // so only such usage is valid:
      // - mainAddress[SubObjectName]
      // this is not valid:
      // - mainAddress[SubObjectName
      // - mainAddressSubObjectName]
      // - mainAddress[SubObjectName]a
      // - mainAddress[]
      if ((indexOfSquareBracket == 0) ||
          (indexOfSquareBracket < 0 && (indexOfClosingSquareBracket >= 0)) ||
          (indexOfSquareBracket > 0 && (indexOfClosingSquareBracket != address.Length - 1)) ||
          (indexOfSquareBracket > 0 && (indexOfClosingSquareBracket - indexOfSquareBracket <= 1))) {
        mainPart = default;
        subObjectName = default;
        return false;
      }

      if (indexOfSquareBracket < 0) {
        mainPart = address;
        subObjectName = default;
        return true;
      }

      mainPart = address.Substring(0, indexOfSquareBracket);
      subObjectName = address.Substring(indexOfSquareBracket + 1, address.Length - indexOfSquareBracket - 2);
      return true;
    }

    /// <summary>
    /// Creates an asset reference from the given address.
    /// </summary>
    /// <param name="address">The address to create the asset reference from.</param>
    /// <returns>The created asset reference.</returns>
    /// <exception cref="System.ArgumentException">Thrown when the main part of the address is not a guid or the address is not valid.</exception>
    public static AssetReference CreateAssetReference(string address) {
      if (TryParseAddress(address, out var mainPart, out var subObjectName)) {
        if (System.Guid.TryParse(mainPart, out _)) {
          // ok, the main part is a guid, can create asset reference
          return new AssetReference(mainPart) {
            SubObjectName = subObjectName,
          };
        } else {
          throw new System.ArgumentException($"The main part of the address is not a guid: {mainPart}", nameof(address));
        }
      } else {
        throw new System.ArgumentException($"Not a valid address: {address}", nameof(address));
      }
    }

#if UNITY_EDITOR
    private static Func<string, Object> s_loadEditorInstance;

    /// <summary>
    /// Loads the editor instance for the given runtime key.
    /// </summary>
    /// <param name="runtimeKey">The runtime key.</param>
    /// <returns>The loaded editor instance.</returns>
    /// <exception cref="System.InvalidOperationException">Thrown when the load editor instance handler is not set.</exception>
    public static Object LoadEditorInstance(string runtimeKey) {
      Assert.Check(s_loadEditorInstance != null, $"Call {nameof(SetLoadEditorInstanceHandler)} before using this method");
      return s_loadEditorInstance(runtimeKey);
    }

    /// <summary>
    /// Sets the load editor instance handler.
    /// </summary>
    /// <param name="loadEditorInstance">The load editor instance handler.</param>
    public static void SetLoadEditorInstanceHandler(Func<string, Object> loadEditorInstance) {
      s_loadEditorInstance = loadEditorInstance;
    }
#endif
  }
}
#endif

#endregion


#region FusionLogInitializer.cs

namespace Fusion {
  using System;
  using UnityEngine;
  
#if UNITY_EDITOR
  using UnityEditor;
  using UnityEditor.Build;
#endif
  
  /// <summary>
  /// Initializes the logging system for Fusion. Use <see cref="InitializeUser"/> to completely override the log level and trace channels or
  /// to provide a custom logger. Use <see cref="InitializeUnityLoggerUser"/> to override default Unity logger settings.
  /// </summary>
  public static partial class FusionLogInitializer {
#if UNITY_EDITOR
    static LogLevel GetEditorLogLevel() {
      var currentBuildTarget = EditorUserBuildSettings.activeBuildTarget;
      var currentBuildTargetGroup = BuildPipeline.GetBuildTargetGroup(currentBuildTarget);
      var currentNamedBuildTarget = NamedBuildTarget.FromBuildTargetGroup(currentBuildTargetGroup);
      var defines = PlayerSettings.GetScriptingDefineSymbols(currentNamedBuildTarget).Split(";");
      
      const string LogLevelNone  = "FUSION_LOGLEVEL_NONE";
      const string LogLevelError = "FUSION_LOGLEVEL_ERROR";
      const string LogLevelWarn  = "FUSION_LOGLEVEL_WARN";
      const string LogLevelInfo  = "FUSION_LOGLEVEL_INFO";
      const string LogLevelDebug = "FUSION_LOGLEVEL_DEBUG";
      const string LogLevelTrace = "FUSION_LOGLEVEL_TRACE";
      
      (string, LogLevel)[] logLevelDefines = {
        (LogLevelNone, LogLevel.None),
        (LogLevelError, LogLevel.Error),
        (LogLevelWarn, LogLevel.Warn),
        (LogLevelInfo, LogLevel.Info),
        (LogLevelDebug, LogLevel.Debug),
      };
      
      string defaultLogLevelDefine = LogLevelInfo;
      
      if (Array.IndexOf(defines, LogLevelTrace) >= 0) {
        FusionEditorLog.Warn($"{LogLevelTrace} is not supported in Fusion. Replacing with {LogLevelDebug}.");
        ArrayUtility.Remove(ref defines, LogLevelTrace);
        defaultLogLevelDefine = LogLevelDebug;
      }
      
      LogLevel? foundLogLevel = null;
      foreach (var (define, logLevel) in logLevelDefines) {
        if (Array.IndexOf(defines, define) < 0) {
          continue;
        }

        foundLogLevel = logLevel;
        break;
      }
      
      if (foundLogLevel == null) {
        if (Application.isPlaying) {
          FusionEditorLog.Log($"No log level define set for Fusion. Setting default: {defaultLogLevelDefine}");
        }
        
        ArrayUtility.Add(ref defines, defaultLogLevelDefine);
        PlayerSettings.SetScriptingDefineSymbols(currentNamedBuildTarget, string.Join(";", defines));
        
        return LogLevel.Info;
      } else {
        return foundLogLevel.Value;
      }
    }
#endif
    
    /// <summary>
    /// Initializes the logging system for Fusion. This method is called automatically when the assembly is loaded.
    /// </summary>
#if UNITY_EDITOR
    [UnityEditor.InitializeOnLoadMethod]
#endif
    [RuntimeInitializeOnLoadMethod]
    public static void Initialize() {
      var isDark = false;
#if UNITY_EDITOR
      isDark = UnityEditor.EditorGUIUtility.isProSkin;
      FusionEditorLog.Initialize(isDark);
#endif
      
      LogLevel logLevel =
#if FUSION_LOGLEVEL_DEBUG || FUSION_LOGLEVEL_TRACE
        LogLevel.Debug;
#elif FUSION_LOGLEVEL_INFO
        LogLevel.Info;
#elif FUSION_LOGLEVEL_WARN
        LogLevel.Warn;
#elif FUSION_LOGLEVEL_ERROR
        LogLevel.Error;
#elif FUSION_LOGLEVEL_NONE
        LogLevel.None;
#elif UNITY_EDITOR
        GetEditorLogLevel();
#else
        LogLevel.None;
      FusionEditorLog.LogWarning($"No log level define set for Fusion, treating as FUSION_LOGLEVEL_NONE (disabled completely).");
#endif
      
      TraceChannels traceChannels = default;
      traceChannels = traceChannels.AddChannelsFromDefines();
      InitializeUser(ref logLevel, ref traceChannels);

      if (Log.IsInitialized) {
        return;
      }

      var logger = CreateLogger(isDarkMode: isDark);
      InitializeUnityLoggerUser(ref logger);
      Log.Initialize(logLevel, logger.CreateLogStream, traceChannels);
    }
    
    static partial void InitializeUser(ref LogLevel logLevel, ref TraceChannels traceChannels);
  }
}

#endregion


#region FusionMppm.cs

namespace Fusion {
  using System;
  using System.Diagnostics;
  using JetBrains.Annotations;
#if FUSION_ENABLE_MPPM
  using System.Collections.Generic;
  using System.IO;
  using System.Linq;
  using System.Text.RegularExpressions;
  using System.Threading;
  using UnityEditor;
#if UNITY_EDITOR
  using UnityEditor.MPE;
#endif
  using UnityEngine;
  using Debug = UnityEngine.Debug;
#endif
  
  // ReSharper disable once IdentifierTypo
  /// <summary>
  /// The current status of MPPM. If the package is not enabled, this will always be <see cref="FusionMppmStatus.Disabled"/>.
  /// </summary>
  public enum FusionMppmStatus {
    /// <summary>
    /// MPPM is not installed.
    /// </summary>
    Disabled,
    /// <summary>
    /// This instance is the main instance. Can use <see cref="FusionMppm.Send{T}"/> to send commands.
    /// </summary>
    MainInstance,
    /// <summary>
    /// This instance is a virtual instance. Will receive commands from the main instance.
    /// </summary>
    VirtualInstance
  }
  
  /// <summary>
  /// Support for Multiplayer Play Mode (MPPM). It uses named pipes
  /// to communicate between the main Unity instance and virtual instances.
  /// </summary>
#if FUSION_ENABLE_MPPM && UNITY_EDITOR
  [InitializeOnLoad]
#endif
  // ReSharper disable once IdentifierTypo
  public partial class FusionMppm {
    
    /// <summary>
    /// The current status of MPPM.
    /// </summary>
    public static readonly FusionMppmStatus Status = FusionMppmStatus.Disabled;
    
    /// <summary>
    /// If <see cref="Status"/> is <see cref="FusionMppmStatus.MainInstance"/>, this static field can be used to send commands.
    /// </summary>
    [CanBeNull]
    public static readonly FusionMppm MainEditor = null;

    /// <summary>
    /// Sends a command to all virtual instances. Use as:
    /// <code>FusionMppm.MainEditor?.Send</code>
    /// </summary>
    /// <param name="data"></param>
    /// <typeparam name="T"></typeparam>
    [Conditional("UNITY_EDITOR")]
    public void Send<T>(T data) where T : FusionMppmCommand {
#if FUSION_ENABLE_MPPM && UNITY_EDITOR
      Assert.Check(Status == FusionMppmStatus.MainInstance, "Only the main instance can send commands");
      BroadcastInternal(data);
#endif
    }

    
    /// <summary>
    /// Broadcasts a command to all virtual instances.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="data"></param>
#if FUSION_ENABLE_MPPM
    [Conditional("UNITY_EDITOR")]
#else
    [Conditional("FUSION_ENABLE_MPPM")]
#endif
    [Obsolete("Use FusionMppm.Broadcaster?.Send instead")]
    public static void Broadcast<T>(T data) where T : FusionMppmCommand {
      MainEditor?.Send(data);
    }

    private FusionMppm() {
      
    }
    
#if FUSION_ENABLE_MPPM && UNITY_EDITOR
    private static readonly string s_mainInstancePath = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
    
    private const string PersistentCommandsFolderPath = "Temp/FusionMppm";
    private const string MpeChannelName = "FusionMppm";
    
    private readonly int _mpeChannelId = ChannelService.ChannelNameToId(MpeChannelName);
    private readonly List<(int connectionId, string guid)> _acks = new List<(int, string)>();
    private readonly Regex _invalidFileCharactersRegex = new Regex(string.Format(@"([{0}]*\.+$)|([{0}]+)", Regex.Escape(new string(Path.GetInvalidFileNameChars()))));
    
    static FusionMppm() {
      
      var indexOfMppmPrefix = Application.dataPath.LastIndexOf("/Library/VP/mppm", StringComparison.OrdinalIgnoreCase);
      Status = indexOfMppmPrefix < 0 ? FusionMppmStatus.MainInstance : FusionMppmStatus.VirtualInstance;
    
      // start MPE (this check is canonical)
      if (!ChannelService.IsRunning()) {
        ChannelService.Start();
      }
      
      FusionEditorLog.TraceMppm($"Status: {Status}, MainInstancePath: {s_mainInstancePath}");
      
      if (Status == FusionMppmStatus.MainInstance) {
        
        MainEditor = new FusionMppm();
        // set up MPE channel
        var disconnect = ChannelService.GetOrCreateChannel(MpeChannelName, MainEditor.ReceiveAck);
        Debug.Assert(disconnect != null);
        
        // ... but since new instances need to e.g. receive all the dependency hashes, set up a folder;
        // it needs to be cleared on every Unity start but survive between domain reloads
        string folderOwnedKey = $"Owns_{PersistentCommandsFolderPath}";
        
        if (Directory.Exists(PersistentCommandsFolderPath) && !SessionState.GetBool(folderOwnedKey, false)) {
          FusionEditorLog.TraceMppm($"Deleting leftover files from {PersistentCommandsFolderPath}");
          foreach (var file in Directory.GetFiles(PersistentCommandsFolderPath)) {
            File.Delete(file);
          }
        }
        
        if (!Directory.Exists(PersistentCommandsFolderPath)) {
          FusionEditorLog.TraceMppm($"Creating command folder {PersistentCommandsFolderPath}");
          Directory.CreateDirectory(PersistentCommandsFolderPath);
        }
        SessionState.SetBool(folderOwnedKey, true);
        
      } else {
        // where is the main instance located?
        s_mainInstancePath = Application.dataPath.Substring(0, indexOfMppmPrefix);
        
        // start the MPE client to await commands
        var client = ChannelClient.GetOrCreateClient(MpeChannelName);
        client.Start(true);
        var disconnect = client.RegisterMessageHandler(data => {
          var json = System.Text.Encoding.UTF8.GetString(data);
          var message = JsonUtility.FromJson<CommandWrapper>(json);
          
          FusionEditorLog.TraceMppm($"Received command {message.Data}");
          message.Data.Execute();
          if (message.Data.NeedsAck) {
            var ack = new AckMessage() {
              Guid = message.Guid
            };
            var ackJson = JsonUtility.ToJson(ack);
            FusionEditorLog.TraceMppm($"Sending ack {ackJson}");
            var ackBytes = System.Text.Encoding.UTF8.GetBytes(ackJson);
            client.Send(ackBytes);
          }
        });
        Debug.Assert(disconnect != null);
        
        // read persistent commands from the main instance
        var mainInstanceCommandsFolderPath = Path.Combine(s_mainInstancePath, PersistentCommandsFolderPath);
        Debug.Assert(Directory.Exists(mainInstanceCommandsFolderPath));
        foreach (var file in Directory.GetFiles(mainInstanceCommandsFolderPath, "*.json")) {
          var json = File.ReadAllText(file);
          var wrapper = JsonUtility.FromJson<CommandWrapper>(json);
          FusionEditorLog.TraceMppm($"Received persistent command {wrapper.Data}");
          wrapper.Data.Execute();
        }
      }
    }
    
    private void BroadcastInternal<T>(T data) where T : FusionMppmCommand {
      Assert.Check(Status == FusionMppmStatus.MainInstance, "Only the main instance can send commands");
      
      var guid = Guid.NewGuid().ToString();
      var wrapper = new CommandWrapper() {
        Guid = guid,
        Data = data
      };
      
      var str   = JsonUtility.ToJson(wrapper);
      var bytes = System.Text.Encoding.UTF8.GetBytes(str);
      
      FusionEditorLog.TraceMppm($"Broadcasting command {str}");
      ChannelService.BroadcastBinary(_mpeChannelId, bytes);

      var persistentKey = data.PersistentKey;
      if (!string.IsNullOrEmpty(persistentKey)) {
        var fileName = $"{_invalidFileCharactersRegex.Replace(persistentKey, "_")}.json";
        var filePath = Path.Combine(PersistentCommandsFolderPath, fileName);
        FusionEditorLog.TraceMppm($"Saving persistent command to {filePath}");
        File.WriteAllText(filePath, str);
      }
      
      if (data.NeedsAck) {
        // well, we need to wait
        var channels = ChannelService.GetChannelClientList();
        // how many acks do we need?
        var numAcks = channels.Count(x => x.name == MpeChannelName);
        WaitForAcks(numAcks, guid);
      }
    }
    
    private void ReceiveAck(int connectionId, byte[] data) {
      var json    = System.Text.Encoding.UTF8.GetString(data);
      var message = JsonUtility.FromJson<AckMessage>(json);
      lock (_acks) {
        _acks.Add((connectionId, message.Guid));
      }
      FusionEditorLog.TraceMppm($"Received ack {json}");
    }
    
    private void WaitForAcks(int numAcks, string guid) {
      var timer   = Stopwatch.StartNew();
      var timeout = TimeSpan.FromSeconds(2);
      
      FusionEditorLog.TraceMppm($"Waiting for {numAcks} acks for {guid}");
      
      while (timer.Elapsed < timeout) {
        for (int i = 0; numAcks > 0 && i < _acks.Count; i++) {
          var ack = _acks[i];
          if (ack.guid == guid) {
            _acks.RemoveAt(i);
            numAcks--;
              
            FusionEditorLog.TraceMppm($"Received ack for {guid} from {ack.connectionId}, {numAcks} left");
          }
        }

        if (numAcks <= 0) {
          FusionEditorLog.TraceMppm($"All acks received");
          return;
        }
          
        FusionEditorLog.TraceMppm($"Waiting for {numAcks} acks");
        ChannelService.DispatchMessages();
        Thread.Sleep(10);
      }
      
      FusionEditorLog.TraceMppm($"Timeout waiting for acks ({numAcks} left)");
    }
    
    [Serializable]
    private class CommandWrapper {
      public string Guid;
      [SerializeReference] public FusionMppmCommand Data;
    }

    [Serializable]
    private class AckMessage {
      public string Guid;
    }
#endif
  }
  
  /// <summary>
  /// The base class for all Fusion MPPM commands.
  /// </summary>
  [Serializable]
  // ReSharper disable once IdentifierTypo
  public abstract class FusionMppmCommand {
    /// <summary>
    /// Execute the command on a virtual instance.
    /// </summary>
    public abstract void Execute();
    /// <summary>
    /// Does the main instance need to wait for an ack?
    /// </summary>
    public virtual bool NeedsAck => false;
    /// <summary>
    /// If the command is persistent (i.e. needs to be executed on each domain reload), this key is used to store it.
    /// </summary>
    public virtual string PersistentKey => null;
  }
}

#endregion


#region FusionMppmRegisterCustomDependencyCommand.cs

#if UNITY_EDITOR
namespace Fusion {
  using System;
  using UnityEngine;

  /// <summary>
  /// A command implementing a workaround for MPPM not syncing custom dependencies.
  /// </summary>
  [Serializable]
  public class FusionMppmRegisterCustomDependencyCommand : FusionMppmCommand {
    /// <summary>
    /// Name of the custom dependency.
    /// </summary>
    public string DependencyName;
    /// <summary>
    /// Hash of the custom dependency.
    /// </summary>
    public string Hash;
      
    /// <inheritdoc cref="FusionMppmCommand.NeedsAck"/>
    public override bool NeedsAck => true;

    /// <inheritdoc cref="FusionMppmCommand.PersistentKey"/>
    public override string PersistentKey => $"Dependency_{DependencyName}";
      
    /// <summary>
    /// Registers a custom dependency with the given name and hash.
    /// </summary>
    public override void Execute() {
      FusionEditorLog.TraceMppm($"Registering custom dependency {DependencyName} with hash {Hash}");
      var hash = Hash128.Parse(Hash);
      UnityEditor.AssetDatabase.RegisterCustomDependency(DependencyName, hash);
    }
  }
}
#endif

#endregion


#region FusionUnityExtensions.cs

namespace Fusion {
#if UNITY_2022_1_OR_NEWER && !UNITY_2022_2_OR_NEWER
  using UnityEngine;
#endif

  /// <summary>
  /// Provides backwards compatibility for Unity API.
  /// </summary>
  public static class FusionUnityExtensions {
    
    #region New Find API

#if UNITY_2022_1_OR_NEWER && !UNITY_2022_2_OR_NEWER 
    public enum FindObjectsInactive {
      Exclude,
      Include,
    }

    public enum FindObjectsSortMode {
      None,
      InstanceID,
    }

    public static T FindFirstObjectByType<T>() where T : Object {
      return (T)FindFirstObjectByType(typeof(T), FindObjectsInactive.Exclude);
    }

    public static T FindAnyObjectByType<T>() where T : Object {
      return (T)FindAnyObjectByType(typeof(T), FindObjectsInactive.Exclude);
    }

    public static T FindFirstObjectByType<T>(FindObjectsInactive findObjectsInactive) where T : Object {
      return (T)FindFirstObjectByType(typeof(T), findObjectsInactive);
    }

    public static T FindAnyObjectByType<T>(FindObjectsInactive findObjectsInactive) where T : Object {
      return (T)FindAnyObjectByType(typeof(T), findObjectsInactive);
    }

    public static Object FindFirstObjectByType(System.Type type, FindObjectsInactive findObjectsInactive) {
      return Object.FindObjectOfType(type, findObjectsInactive == FindObjectsInactive.Include);
    }

    public static Object FindAnyObjectByType(System.Type type, FindObjectsInactive findObjectsInactive) {
      return Object.FindObjectOfType(type, findObjectsInactive == FindObjectsInactive.Include);
    }

    public static T[] FindObjectsByType<T>(FindObjectsSortMode sortMode) where T : Object {
      return ConvertObjects<T>(FindObjectsByType(typeof(T), FindObjectsInactive.Exclude, sortMode));
    }

    public static T[] FindObjectsByType<T>(
      FindObjectsInactive findObjectsInactive,
      FindObjectsSortMode sortMode)
      where T : Object {
      return ConvertObjects<T>(FindObjectsByType(typeof(T), findObjectsInactive, sortMode));
    }

    public static Object[] FindObjectsByType(System.Type type, FindObjectsSortMode sortMode) {
      return FindObjectsByType(type, FindObjectsInactive.Exclude, sortMode);
    }

    public static Object[] FindObjectsByType(System.Type type, FindObjectsInactive findObjectsInactive, FindObjectsSortMode sortMode) {
      return Object.FindObjectsOfType(type, findObjectsInactive == FindObjectsInactive.Include);
    }

    static T[] ConvertObjects<T>(Object[] rawObjects) where T : Object {
      if (rawObjects == null)
        return (T[])null;
      T[] objArray = new T[rawObjects.Length];
      for (int index = 0; index < objArray.Length; ++index)
        objArray[index] = (T)rawObjects[index];
      return objArray;
    }

#endif

    #endregion
  }
}

#endregion



#endregion


#region Assets/Photon/Fusion/Runtime/NetworkObjectBaker.cs

﻿//#undef UNITY_EDITOR
namespace Fusion {
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using System.Text;
  using System.Threading.Tasks;
  using UnityEngine;

#if UNITY_EDITOR
  using UnityEditor;
#endif

  public class NetworkObjectBaker {

    private List<NetworkObject> _allNetworkObjects             = new List<NetworkObject>();
    private List<TransformPath> _networkObjectsPaths           = new List<TransformPath>();
    private List<SimulationBehaviour> _allSimulationBehaviours = new List<SimulationBehaviour>();
    private TransformPathCache _pathCache                      = new TransformPathCache();
    private List<NetworkBehaviour> _arrayBufferNB    = new List<NetworkBehaviour>();
    private List<NetworkObject> _arrayBufferNO       = new List<NetworkObject>();
    
    public struct Result {
      public bool HadChanges { get; }
      public int ObjectCount { get; }
      public int BehaviourCount { get; }

      public Result(bool dirty, int objectCount, int behaviourCount) {
        HadChanges = dirty;
        ObjectCount = objectCount;
        BehaviourCount = behaviourCount;
      }
    }

    protected virtual void SetDirty(MonoBehaviour obj) {
      // do nothing
    }

    protected virtual bool TryGetExecutionOrder(MonoBehaviour obj, out int order) {
      order = default;
      return false;
    }
    
    protected virtual uint GetSortKey(NetworkObject obj) {
      return 0;
    }

    /// <summary>
    /// Postprocesses the behaviour. Returns true if the object was marked dirty.
    /// </summary>
    /// <param name="behaviour"></param>
    /// <returns></returns>
    protected virtual bool PostprocessBehaviour(SimulationBehaviour behaviour) {
      // do nothing
      return false;
    }
    
    [System.Diagnostics.Conditional("FUSION_EDITOR_TRACE")]
    protected static void Trace(string msg) {
      Debug.Log($"[Fusion/NetworkObjectBaker] {msg}");
    }

    protected static void Warn(string msg, UnityEngine.Object context = null) {
      Debug.LogWarning($"[Fusion/NetworkObjectBaker] {msg}", context);
    }

    public Result Bake(GameObject root) {

      if (root == null) {
        throw new ArgumentNullException(nameof(root));
      }
      
      root.GetComponentsInChildren(true, _allNetworkObjects);
      
      // remove null ones (missing scripts may cause that)
      _allNetworkObjects.RemoveAll(x => x == null);
      
      if (_allNetworkObjects.Count == 0) {
        return new Result(false, 0, 0);
      }

      try {
        foreach (var obj in _allNetworkObjects) {
          _networkObjectsPaths.Add(_pathCache.Create(obj.transform));
        }

        bool dirty = false;
        
        _allNetworkObjects.Reverse();
        _networkObjectsPaths.Reverse();

        root.GetComponentsInChildren(true, _allSimulationBehaviours);
        _allSimulationBehaviours.RemoveAll(x => x == null);
        
        int countNO = _allNetworkObjects.Count;
        int countSB = _allSimulationBehaviours.Count;

        // start from the leaves
        for (int i = 0; i < _allNetworkObjects.Count; ++i) {
          var obj = _allNetworkObjects[i];

          var objDirty = false;
          var objActive = obj.gameObject.activeInHierarchy;
          int? objExecutionOrder = null;
          if (!objActive) {
            if (TryGetExecutionOrder(obj, out var order)) {
              objExecutionOrder = order;
            } else {
              Warn($"Unable to get execution order for {obj}. " +
                $"Because the object is initially inactive, Fusion is unable to guarantee " +
                $"the script's Awake will be invoked before Spawned. Please implement {nameof(TryGetExecutionOrder)}.");
            }
          }

          // find nested behaviours
          _arrayBufferNB.Clear();
          
          var path = _networkObjectsPaths[i];
          
          string entryPath = path.ToString();
          for (int scriptIndex = _allSimulationBehaviours.Count - 1; scriptIndex >= 0; --scriptIndex) {
            var script = _allSimulationBehaviours[scriptIndex];
            var scriptPath = _pathCache.Create(script.transform);

            if (_pathCache.IsEqualOrAncestorOf(path, scriptPath)) {
              if (script is NetworkBehaviour nb) {
                _arrayBufferNB.Add(nb);
              }
              
              objDirty |= PostprocessBehaviour(script);
              
              _allSimulationBehaviours.RemoveAt(scriptIndex);

              if (objExecutionOrder != null) {
                // check if execution order is ok
                if (TryGetExecutionOrder(script, out var scriptOrder)) {
                  if (objExecutionOrder <= scriptOrder) {
                    Warn($"{obj} execution order is less or equal than of the script {script}. " +
                      $"Because the object is initially inactive, Spawned callback will be invoked before the script's Awake on activation.", script);
                  }
                } else {
                  Warn($"Unable to get execution order for {script}. " +
                    $"Because the object is initially inactive, Fusion is unable to guarantee " +
                    $"the script's Awake will be invoked before Spawned. Please implement {nameof(TryGetExecutionOrder)}.");
                }
              }

            } else if (_pathCache.Compare(path, scriptPath) < 0) {
              // can't discard it yet
            } else {
              Debug.Assert(_pathCache.Compare(path, scriptPath) > 0);
              break;
            }
          }

          _arrayBufferNB.Reverse();
          objDirty |= Set(obj, ref obj.NetworkedBehaviours, _arrayBufferNB);

          // handle flags

          var flags = obj.Flags;

          if (!flags.IsVersionCurrent()) {
            flags = flags.SetCurrentVersion();
          }

          objDirty |= Set(obj, ref obj.Flags, flags);

          // what's left is nested network objects resolution
          {
            _arrayBufferNO.Clear();

            // collect descendants; descendants should be continous without gaps here
            int j = i - 1;
            for (; j >= 0 && _pathCache.IsAncestorOf(path, _networkObjectsPaths[j]); --j) {
              _arrayBufferNO.Add(_allNetworkObjects[j]);
            }

            int descendantsBegin = j + 1;
            Debug.Assert(_arrayBufferNO.Count == i - descendantsBegin);

            objDirty |= Set(obj, ref obj.NestedObjects, _arrayBufferNO);
          }

          objDirty |= Set(obj, ref obj.SortKey, GetSortKey(obj));
          
          if (objDirty) {
            SetDirty(obj);
            dirty = true;
          }
        }

        return new Result(dirty, countNO, countSB);
      } finally {
        _pathCache.Clear();
        _allNetworkObjects.Clear();
        _allSimulationBehaviours.Clear();

        _networkObjectsPaths.Clear();

        _arrayBufferNB.Clear();
        _arrayBufferNO.Clear();
      }
    }

    private bool Set<T>(MonoBehaviour host, ref T field, T value) {
      if (!EqualityComparer<T>.Default.Equals(field, value)) {
        Trace($"Object dirty: {host} ({field} vs {value})");
        field = value;
        return true;
      } else {
        return false;
      }
    }

    private bool Set<T>(MonoBehaviour host, ref T[] field, List<T> value) {
      var comparer = EqualityComparer<T>.Default;
      if (field == null || field.Length != value.Count || !field.SequenceEqual(value, comparer)) {
        Trace($"Object dirty: {host} ({field} vs {value})");
        field = value.ToArray();
        return true;
      } else {
        return false;
      }
    }

    public unsafe readonly struct TransformPath {
      public const int MaxDepth = 10;

      public struct _Indices {
        public fixed ushort Value[MaxDepth];
      }

      public readonly _Indices Indices;
      public readonly ushort Depth;
      public readonly ushort Next;

      internal TransformPath(ushort depth, ushort next, List<ushort> indices, int offset, int count) {
        Depth = depth;
        Next = next;

        for (int i = 0; i < count; ++i) {
          Indices.Value[i] = indices[i + offset];
        }
      }

      public override string ToString() {
        var builder = new StringBuilder();
        for (int i = 0; i < Depth && i < MaxDepth; ++i) {
          if (i > 0) {
            builder.Append("/");
          }
          builder.Append(Indices.Value[i]);
        }

        if (Depth > MaxDepth) {
          Debug.Assert(Next > 0);
          builder.Append($"/...[{Depth - MaxDepth}]");
        }

        return builder.ToString();
      }
    }

    public sealed unsafe class TransformPathCache : IComparer<TransformPath>, IEqualityComparer<TransformPath> {

      private Dictionary<Transform, TransformPath> _cache = new Dictionary<Transform, TransformPath>();
      private List<ushort> _siblingIndexStack             = new List<ushort>();
      private List<TransformPath> _nexts                  = new List<TransformPath>();


      public TransformPath Create(Transform transform) {
        if (_cache.TryGetValue(transform, out var existing)) {
          return existing;
        }

        _siblingIndexStack.Clear();
        for (var tr = transform; tr != null; tr = tr.parent) {
          _siblingIndexStack.Add(checked((ushort)tr.GetSiblingIndex()));
        }
        _siblingIndexStack.Reverse();


        var depth = checked((ushort)_siblingIndexStack.Count);

        ushort nextPlusOne = 0;

        if (depth > TransformPath.MaxDepth) {

          int i;
          if (depth % TransformPath.MaxDepth != 0) {
            // tail is going to be partially full
            i = depth - (depth % TransformPath.MaxDepth);
          } else {
            // tail is going to be full
            i = depth - TransformPath.MaxDepth;
          }

          for (; i > 0; i -= TransformPath.MaxDepth) {
            checked {
              TransformPath path = new TransformPath((ushort)(depth - i), nextPlusOne,
                _siblingIndexStack, i, Mathf.Min(TransformPath.MaxDepth, depth - i));
              _nexts.Add(path);
              nextPlusOne = (ushort)_nexts.Count;
            }
          }
        }

        var result = new TransformPath(depth, nextPlusOne,
          _siblingIndexStack, 0, Mathf.Min(TransformPath.MaxDepth, depth));

        _cache.Add(transform, result);
        return result;
      }

      public void Clear() {
        _nexts.Clear();
        _cache.Clear();
        _siblingIndexStack.Clear();
      }

      public bool Equals(TransformPath x, TransformPath y) {
        if (x.Depth != y.Depth) {
          return false;
        }

        return CompareToDepthUnchecked(x, y, x.Depth) == 0;
      }

      public int GetHashCode(TransformPath obj) {
        int hash = obj.Depth;
        return GetHashCode(obj, hash);
      }

      public int Compare(TransformPath x, TransformPath y) {
        var diff = CompareToDepthUnchecked(x, y, Mathf.Min(x.Depth, y.Depth));
        if (diff != 0) {
          return diff;
        }

        return x.Depth - y.Depth;
      }

      private int CompareToDepthUnchecked(in TransformPath x, in TransformPath y, int depth) {
        for (int i = 0; i < depth && i < TransformPath.MaxDepth; ++i) {
          int diff = x.Indices.Value[i] - y.Indices.Value[i];
          if (diff != 0) {
            return diff;
          }
        }

        if (depth > TransformPath.MaxDepth) {
          Debug.Assert(x.Next > 0);
          Debug.Assert(y.Next > 0);
          return CompareToDepthUnchecked(_nexts[x.Next - 1], _nexts[y.Next - 1], depth - TransformPath.MaxDepth);
        } else {
          return 0;
        }
      }

      private int GetHashCode(in TransformPath path, int hash) {
        for (int i = 0; i < path.Depth && i < TransformPath.MaxDepth; ++i) {
          hash = hash * 31 + path.Indices.Value[i];
        }

        if (path.Depth > TransformPath.MaxDepth) {
          Debug.Assert(path.Next > 0);
          hash = GetHashCode(_nexts[path.Next - 1], hash);
        }

        return hash;
      }

      public bool IsAncestorOf(in TransformPath x, in TransformPath y) {
        if (x.Depth >= y.Depth) {
          return false;
        }

        return CompareToDepthUnchecked(x, y, x.Depth) == 0;
      }

      public bool IsEqualOrAncestorOf(in TransformPath x, in TransformPath y) {
        if (x.Depth > y.Depth) {
          return false;
        }

        return CompareToDepthUnchecked(x, y, x.Depth) == 0;
      }

      public string Dump(in TransformPath x) {
        var builder = new StringBuilder();

        Dump(x, builder);

        return builder.ToString();
      }

      private void Dump(in TransformPath x, StringBuilder builder) {
        for (int i = 0; i < x.Depth && i < TransformPath.MaxDepth; ++i) {
          if (i > 0) {
            builder.Append("/");
          }
          builder.Append(x.Indices.Value[i]);
        }

        if (x.Depth > TransformPath.MaxDepth) {
          Debug.Assert(x.Next > 0);
          builder.Append("/");
          Dump(_nexts[x.Next - 1], builder);
        }
      }
    }
  }
}


#endregion


#region Assets/Photon/Fusion/Runtime/NetworkPrefabSourceUnity.cs

﻿namespace Fusion {
  using System;
  using Object = UnityEngine.Object;

  [Serializable]
  public class NetworkPrefabSourceStatic : NetworkAssetSourceStatic<NetworkObject>, INetworkPrefabSource {
    public NetworkObjectGuid               AssetGuid;
    NetworkObjectGuid INetworkPrefabSource.AssetGuid => AssetGuid;
  }
  
  [Serializable]
  public class NetworkPrefabSourceStaticLazy : NetworkAssetSourceStaticLazy<NetworkObject>, INetworkPrefabSource {
    public NetworkObjectGuid               AssetGuid;
    NetworkObjectGuid INetworkPrefabSource.AssetGuid => AssetGuid;
  }

  [Serializable]
  public class NetworkPrefabSourceResource : NetworkAssetSourceResource<NetworkObject>, INetworkPrefabSource {
    public NetworkObjectGuid               AssetGuid;
    NetworkObjectGuid INetworkPrefabSource.AssetGuid => AssetGuid;
  }
  
#if FUSION_ENABLE_ADDRESSABLES && !FUSION_DISABLE_ADDRESSABLES
  [Serializable]
  public class NetworkPrefabSourceAddressable : NetworkAssetSourceAddressable<NetworkObject>, INetworkPrefabSource {
    public NetworkObjectGuid               AssetGuid;
    NetworkObjectGuid INetworkPrefabSource.AssetGuid => AssetGuid;
  }
#endif
}

#endregion


#region Assets/Photon/Fusion/Runtime/Statistics/FusionStatisticsHelper.cs

namespace Fusion.Statistics {
  using System;
  using UnityEngine;

  internal static class FusionStatisticsHelper {
    public const float DEFAULT_GRAPH_HEIGHT = 150F;
    public const float DEFAULT_HEADER_HEIGHT = 50F;
    
    internal static void GetStatGraphDefaultSettings(RenderSimStats stat, out string valueTextFormat, out float valueTextMultiplier, out bool ignoreZeroOnAverage, out bool ignoreZeroOnBuffer, out int accumulateTimeMs) {

      valueTextFormat = "{0:0}";
      valueTextMultiplier = 1f;
      ignoreZeroOnAverage = false; 
      ignoreZeroOnBuffer = false;
      accumulateTimeMs = 0; // Default is every update, so zero.
      
      switch (stat) {
            case RenderSimStats.InPackets:
            case RenderSimStats.OutPackets:
            case RenderSimStats.InObjectUpdates:
            case RenderSimStats.OutObjectUpdates:
              valueTextFormat = "{0:0}";
              accumulateTimeMs = 1000;
              break;
            
            case RenderSimStats.RTT:
              valueTextFormat = "{0:0} ms";
              valueTextMultiplier = 1000;
              ignoreZeroOnAverage = true; ignoreZeroOnBuffer = true;
              break;
            
            case RenderSimStats.InBandwidth:
            case RenderSimStats.OutBandwidth:
            case RenderSimStats.InputInBandwidth:
            case RenderSimStats.InputOutBandwidth:
              valueTextFormat = "{0:0} B";
              accumulateTimeMs = 1000;
              break;
            
            case RenderSimStats.AverageInPacketSize:
            case RenderSimStats.AverageOutPacketSize:
              valueTextFormat = "{0:0} B";
              ignoreZeroOnBuffer = true;
              ignoreZeroOnAverage = true;
              break;
            
            case RenderSimStats.Resimulations:
              valueTextFormat = "{0:0}";
              break;
            case RenderSimStats.ForwardTicks:
              valueTextFormat = "{0:0}";
              break;
            
            case RenderSimStats.TimeResets:
            case RenderSimStats.SimulationSpeed:
            case RenderSimStats.InterpolationSpeed:
              valueTextFormat = "{0:0}";
              break;
            
            // All time stats are normalized to use seconds, so 1000 multiplier to be ms.
            case RenderSimStats.InputReceiveDelta:
            case RenderSimStats.StateReceiveDelta:
            case RenderSimStats.SimulationTimeOffset:
            case RenderSimStats.InterpolationOffset:
              valueTextMultiplier = 1000;
              valueTextFormat = "{0:0} ms";
              break;
            
            case RenderSimStats.GeneralAllocatedMemoryInUse:
            case RenderSimStats.ObjectsAllocatedMemoryInUse:
            case RenderSimStats.ObjectsAllocatedMemoryFree:
            case RenderSimStats.GeneralAllocatedMemoryFree:
              valueTextFormat = "{0:0} B";
              break;
            
            case RenderSimStats.WordsWrittenCount:
            case RenderSimStats.WordsReadCount:
              valueTextFormat = "{0:0}";
              ignoreZeroOnBuffer = true;
              accumulateTimeMs = 1000;
              break;
            case RenderSimStats.WordsWrittenSize:
            case RenderSimStats.WordsReadSize:
              valueTextFormat = "{0:0} B";
              ignoreZeroOnBuffer = true;
              accumulateTimeMs = 1000;
              break;
            
            default:
              valueTextFormat = "{0:0}";
              break;
          }
    }

    internal static float GetStatDataFromSnapshot(RenderSimStats stat, FusionStatisticsSnapshot simulationStatsSnapshot) {
      switch (stat) {
            // Sim stats
            case RenderSimStats.InPackets:
              return simulationStatsSnapshot.InPackets;
            case RenderSimStats.OutPackets:
              return simulationStatsSnapshot.OutPackets;
            case RenderSimStats.RTT:
              return simulationStatsSnapshot.RoundTripTime;
            case RenderSimStats.InBandwidth:
              return simulationStatsSnapshot.InBandwidth;
            case RenderSimStats.OutBandwidth:
              return simulationStatsSnapshot.OutBandwidth;
            case RenderSimStats.Resimulations:
              return simulationStatsSnapshot.Resimulations;
            case RenderSimStats.ForwardTicks:
              return simulationStatsSnapshot.ForwardTicks;
            case RenderSimStats.InputInBandwidth:
              return simulationStatsSnapshot.InputInBandwidth;
            case RenderSimStats.InputOutBandwidth:
              return simulationStatsSnapshot.InputOutBandwidth;
            case RenderSimStats.AverageInPacketSize:
              return simulationStatsSnapshot.InBandwidth / Mathf.Max(simulationStatsSnapshot.InPackets, 1);
            case RenderSimStats.AverageOutPacketSize:
              return simulationStatsSnapshot.OutBandwidth / Mathf.Max(simulationStatsSnapshot.OutPackets, 1);
            case RenderSimStats.InObjectUpdates:
              return simulationStatsSnapshot.InObjectUpdates;
            case RenderSimStats.OutObjectUpdates:
              return simulationStatsSnapshot.OutObjectUpdates;
            case RenderSimStats.ObjectsAllocatedMemoryInUse:
              return simulationStatsSnapshot.ObjectsAllocMemoryUsedInBytes;
            case RenderSimStats.GeneralAllocatedMemoryInUse:
              return simulationStatsSnapshot.GeneralAllocMemoryUsedInBytes;
            case RenderSimStats.ObjectsAllocatedMemoryFree:
              return simulationStatsSnapshot.ObjectsAllocMemoryFreeInBytes;
            case RenderSimStats.GeneralAllocatedMemoryFree:
              return simulationStatsSnapshot.GeneralAllocMemoryFreeInBytes;
            case RenderSimStats.WordsWrittenCount:
              return simulationStatsSnapshot.WordsWrittenCount;
            case RenderSimStats.WordsWrittenSize:
              return simulationStatsSnapshot.WordsWrittenSize;
            case RenderSimStats.WordsReadCount:
              return simulationStatsSnapshot.WordsReadCount;
            case RenderSimStats.WordsReadSize:
              return simulationStatsSnapshot.WordsReadSize;
            
            // Time stats
            case RenderSimStats.InputReceiveDelta:
              return simulationStatsSnapshot.InputReceiveDelta;
            case RenderSimStats.TimeResets:
              return simulationStatsSnapshot.TimeResets;
            case RenderSimStats.StateReceiveDelta:
              return simulationStatsSnapshot.StateReceiveDelta;
            case RenderSimStats.SimulationTimeOffset:
              return simulationStatsSnapshot.SimulationTimeOffset;
            case RenderSimStats.SimulationSpeed:
              return simulationStatsSnapshot.SimulationSpeed;
            case RenderSimStats.InterpolationOffset:
              return simulationStatsSnapshot.InterpolationOffset;
            case RenderSimStats.InterpolationSpeed:
              return simulationStatsSnapshot.InterpolationSpeed;
          }
          
          return default;
    }
  }
}

#endregion


#region Assets/Photon/Fusion/Runtime/Statistics/FusionStatsGraphBase.cs

namespace Fusion.Statistics {
  using UnityEngine;
  using UnityEngine.UI;
  using System;
  using System.Globalization;

  public abstract partial class FusionStatsGraphBase : MonoBehaviour {
    
    private static readonly int Samples = Shader.PropertyToID(SHADER_PROPERTY_SAMPLES);
    private static readonly IFormatProvider _formatProvider = CultureInfo.GetCultureInfo("en-US");

    private const string SHADER_PROPERTY_VALUES = "_Values";
    private const string SHADER_PROPERTY_SAMPLES = "_Samples";
    private const string SHADER_PROPERTY_THRESHOLD_1 = "_Threshold1";
    private const string SHADER_PROPERTY_THRESHOLD_2 = "_Threshold2";
    private const string SHADER_PROPERTY_THRESHOLD_3 = "_Threshold3";
    private const string SHADER_PROPERTY_AVERAGE = "_Average";

    private int _valuesShaderPropertyID = Shader.PropertyToID(SHADER_PROPERTY_VALUES);
    private int _threshold1ShaderPropertyID = Shader.PropertyToID(SHADER_PROPERTY_THRESHOLD_1);
    private int _threshold2ShaderPropertyID = Shader.PropertyToID(SHADER_PROPERTY_THRESHOLD_2);
    private int _threshold3ShaderPropertyID = Shader.PropertyToID(SHADER_PROPERTY_THRESHOLD_3);
    private int _averageShaderPropertyID = Shader.PropertyToID(SHADER_PROPERTY_AVERAGE);

    // serialize private
    [SerializeField] private RectTransform _render;
    [SerializeField] private RectTransform _header;
    [SerializeField] private Image _targetImage;
    [SerializeField] private Button _toggleButton;
    [SerializeField] private Text _averageValueText;
    [SerializeField] private Text _peakValueText;
    [SerializeField] private Text _currentValueText;
    [Space] [SerializeField] private Text _threshold1Text;
    [SerializeField] private Text _threshold2Text;
    [SerializeField] private Text _threshold3Text;
    
    //protected
    [Space] [SerializeField] protected float _valueTextMultiplier = 1f;
    [SerializeField] [Range(60, 540)] protected int _maxSamples = 300;
    [SerializeField] protected float _threshold1;
    [SerializeField] protected float _threshold2;
    [SerializeField] protected float _threshold3;
    [SerializeField] protected bool _ignoreZeroedValuesOnAverageCalculation;
    [SerializeField] protected bool _ignoreZeroedValuesOnBuffer;
    [SerializeField] protected float _valuesTextUpdateDelay = .1f;

    private FusionStatBuffer _bufferValues;
    private float[] _bufferNormalizedValues;

    private float _headerHeight = 25;
    private float _renderHeight = 125;
    private VerticalLayoutGroup _parentLayoutGroup;

    private float _invertedRenderMaxValue;
    private float _lastUpdateTime;
    private Material _material;

    private bool Initialized => _bufferNormalizedValues != null;

    protected virtual void Initialize(int accumulateTimeMs) {
      _material = new Material(_targetImage.material);
      _targetImage.material = _material;
      _bufferValues = new FusionStatBuffer(_maxSamples, _ignoreZeroedValuesOnAverageCalculation, accumulateTimeMs);
      _bufferNormalizedValues = new float[_maxSamples];
      _parentLayoutGroup = GetComponentInParent<VerticalLayoutGroup>();

      _lookupTable = null;
      _lookupMultiplier = 1.0f;

      switch (_valueTextFormat) {
        case "{0:0}": {
          _lookupTable = LOOKUP_TABLE_0;
          _lookupMultiplier = 1.0f;
          break;
        }
        case "{0:0} ms": {
          _lookupTable = LOOKUP_TABLE_0ms;
          _lookupMultiplier = 1.0f;
          break;
        }
        case "{0:0} B": {
          _lookupTable = LOOKUP_TABLE_0_BYTES;
          _lookupMultiplier = 1.0f;
          break;
        }
        case "{0:0.00} ms": {
          _lookupTable = LOOKUP_TABLE_0_00ms;
          _lookupMultiplier = 100.0f;
          break;
        }
      }

      Restore();
    }

    protected virtual void OnEnable() {
      var statsRender = GetComponentInParent<FusionStatistics>(true);
      if (statsRender) {
        statsRender.RegisterGraph(this);
        Restore();
      }
    }

    protected virtual void OnDisable() {
      var statsRender = GetComponentInParent<FusionStatistics>(true);
      if (statsRender) {
        statsRender.UnregisterGraph(this);
        Restore();
      }
    }

    protected virtual void AddValueToBuffer(float value, ref DateTime now) {
      if (_ignoreZeroedValuesOnBuffer && value == 0) return;
      
      _bufferValues.Add(value, ref now);

      _invertedRenderMaxValue = 1 / _bufferValues.MaxValue;

      _invertedRenderMaxValue *= .9f; // 10 % more to fell better on render

      for (int i = 0, k = _bufferValues.Index; i < _maxSamples; i++, k = (k+1)%_bufferValues.Length) {
        _bufferNormalizedValues[i] = _bufferValues[k] * _invertedRenderMaxValue;
      }
      
      SetGraphValues(_bufferNormalizedValues);
      OnSetValues();
    }

    protected virtual void Refit() {
      var finalHeight = 0f;
      var rect = (RectTransform)transform;

      if (_render.gameObject.activeSelf)
        finalHeight += _renderHeight;
      if (_header.gameObject.activeSelf)
        finalHeight += _headerHeight;

      rect.sizeDelta = new Vector2(rect.sizeDelta.x, finalHeight);
      _parentLayoutGroup.enabled = false;
      _parentLayoutGroup.enabled = true;
    }

    protected virtual void Restore() {
      if (Initialized == false) return;
      
      _material.SetInteger(Samples, _maxSamples);
      // The normalized one needs to be cleaned.
      Array.Clear(_bufferNormalizedValues, 0, _maxSamples);
      Refit();
    }

    public virtual void ToggleRenderDisplay() {
      var active = _render.gameObject.activeSelf;
      _render.gameObject.SetActive(!active);

      if (active) {
        OnDisable();
        _toggleButton.transform.rotation = Quaternion.Euler(0, 0, 90);
      } else {
        _toggleButton.transform.rotation = Quaternion.identity;
        OnEnable();
      }

      Refit();
    }

    protected virtual void OnSetValues() {
      if (Time.time >= _lastUpdateTime + _valuesTextUpdateDelay) {
        _lastUpdateTime = Time.time;

        _averageValueText.text = GetValueText(_bufferValues.AverageValue * _valueTextMultiplier);
        _peakValueText.text = GetValueText(_bufferValues.MaxValue * _valueTextMultiplier);
      }
      
      _currentValueText.text = GetValueText(_bufferValues.LatestValue * _valueTextMultiplier);

      float normalizedThreshold1 = _threshold1 * _invertedRenderMaxValue;
      float normalizedThreshold2 = _threshold2 * _invertedRenderMaxValue;
      float normalizedThreshold3 = _threshold3 * _invertedRenderMaxValue;

      _material.SetFloat(_threshold1ShaderPropertyID, normalizedThreshold1);
      _material.SetFloat(_threshold2ShaderPropertyID, normalizedThreshold2);
      _material.SetFloat(_threshold3ShaderPropertyID, normalizedThreshold3);

      _threshold1Text.text = GetValueText(_threshold1 * _valueTextMultiplier);
      _threshold2Text.text = GetValueText(_threshold2 * _valueTextMultiplier);
      _threshold3Text.text = GetValueText(_threshold3 * _valueTextMultiplier);

      UpdateThresholdPosition(_threshold1Text, normalizedThreshold1);
      UpdateThresholdPosition(_threshold2Text, normalizedThreshold2);
      UpdateThresholdPosition(_threshold3Text, normalizedThreshold3);
    }
    
    protected void SetThresholds(float threshold1, float threshold2, float threshold3) {
      _threshold1 = threshold1 / _valueTextMultiplier;
      _threshold2 = threshold2 / _valueTextMultiplier;
      _threshold3 = threshold3 / _valueTextMultiplier;
    }

    protected void SetIgnoreZeroValues(bool ignoreZeroOnAverage, bool ignoreZeroOnBuffer) {
      _ignoreZeroedValuesOnAverageCalculation = ignoreZeroOnAverage;
      _ignoreZeroedValuesOnBuffer = ignoreZeroOnBuffer;
      _bufferValues.SetIgnoreZeroOnAverage(ignoreZeroOnAverage);
    }

    protected void SetValueTextFormat(string value) {
      _valueTextFormat = value;
    }

    protected void SetValueTextMultiplier(float value) {
      _valueTextMultiplier = value;
    }

    protected void SetAccumulateTime(int accumulateTimeMs) {
      _bufferValues.SetAccumulateTime(accumulateTimeMs);
    }

    private void UpdateThresholdPosition(Text text, float thresholdNormalized) {
      Vector3 position = text.rectTransform.anchoredPosition3D;
      var renderHalfHeight = _targetImage.rectTransform.rect.height * .5f;

      position.y = RemapValue(thresholdNormalized, 0, 1, -renderHalfHeight, renderHalfHeight);
      text.rectTransform.anchoredPosition3D = position;
      text.gameObject.SetActive(thresholdNormalized < 1 && thresholdNormalized > 0);
    }

    protected virtual void SetGraphValues(float[] values) {
      if (values == null || values.Length == 0)
        return;

      _material.SetFloat(_averageShaderPropertyID, _bufferValues.AverageValue);
      _material.SetFloatArray(_valuesShaderPropertyID, values);
    }

    private float RemapValue(float value, float iMin, float iMax, float oMin, float oMax) {
      if (float.IsNaN(value)) return oMin;

      var t = Mathf.InverseLerp(iMin, iMax, value);
      return Mathf.Lerp(oMin, oMax, t);
    }

    public abstract void UpdateGraph(NetworkRunner runner, FusionStatisticsManager statisticsManager, ref DateTime now);

    internal struct FusionStatBuffer {
      private readonly float[] _buffer;
      private int _index;
      private int _count;
      private int _zeroCount;
      private bool _ignoreZeroOnAverage;
      private TimeSpan _accumulateTimeSpan;

      private float _sum;
      private float _max;
      private float _accumulated;
      private DateTime _lastBufferInsertTime;

      public int Index => _index;
      public int Length => _buffer.Length;
      public float MaxValue => _max;


      public FusionStatBuffer(int size, bool ignoreZeroOnAverage, int accumulateTimeMs) {
        _buffer = new float[size];
        _index = 0;
        _count = 0;
        _zeroCount = 0;
        _ignoreZeroOnAverage = ignoreZeroOnAverage;
        _accumulateTimeSpan = TimeSpan.FromMilliseconds(accumulateTimeMs);
        _sum = 0;
        _max = float.MinValue;
        _accumulated = 0;
        _lastBufferInsertTime = DateTime.MinValue;
      }
      
      public void SetAccumulateTime(int accumulateTimeMs) {
        _accumulateTimeSpan = TimeSpan.FromMilliseconds(accumulateTimeMs);
      }

      public void SetIgnoreZeroOnAverage(bool value) {
        _ignoreZeroOnAverage = value;
      }

      public float this[int index] => _buffer[index];

      public void Add(float value, ref DateTime now) {

        _accumulated += value;
        
        if (now - _lastBufferInsertTime >= _accumulateTimeSpan) {
          AddOnBuffer(_accumulated);
          _accumulated = 0;
          _lastBufferInsertTime = now;
        } 
      }

      private void AddOnBuffer(float value) {
         
        var recalculateMax = false;
        
        if (_count == _buffer.Length) {
          var removingValue = _buffer[_index];
          _sum -= removingValue;
          
          if (removingValue == 0)
            _zeroCount = Mathf.Max(0, _zeroCount-1);

          if (removingValue >= _max) {
            recalculateMax = true;
          }
        } else {
          _count++;
        }

        if (value == 0)
          _zeroCount = Mathf.Min(_count-1, _zeroCount+1);

        _buffer[_index] = value;
        
        _sum += value;
        
        if (value > _max) {
          _max = value;
        }

        _index = (_index + 1) % _buffer.Length;

        if (recalculateMax) {
          _max = CalculateMax();
        }
      }

      public float LatestValue {
        get {
          if (_count == 0)
            return 0;
          return _buffer[(_index - 1 + _buffer.Length) % _buffer.Length];
        }
      }

      public float AverageValue {
        get {
          if (_count == 0)
            return 0f;
            
          return _sum / (_ignoreZeroOnAverage ? _count - _zeroCount : _count);
        }
      }

      private float CalculateMax()
      {
        float max = float.MinValue;
        for (int i = 0; i < _count; i++) {
          if (_buffer[i] > max) {
            max = _buffer[i];
          }
        }
        return max;
      }
    }
  }
}

#endregion


#region Assets/Photon/Fusion/Runtime/Statistics/FusionStatsLookup.cs

namespace Fusion.Statistics {
using UnityEngine;

  public partial class FusionStatsGraphBase {
    
    [SerializeField]
    private string _valueTextFormat     = "{0}";
    private string[][] _lookupTable;
    private float      _lookupMultiplier;
    
    private string GetValueText(float value)
    {
      if (_lookupTable != null)
      {
        int rows    = _lookupTable.Length;
        int columns = _lookupTable[0].Length;

        int intValue = Mathf.RoundToInt(value * _lookupMultiplier);
        if (intValue >= 0 && intValue < rows * columns)
        {
          int row    = intValue % rows;
          int column = intValue / rows;

          return _lookupTable[row][column];
        }
      }

      return string.Format(_formatProvider, _valueTextFormat, value);
    }

    private static readonly string[][] LOOKUP_TABLE_0 =
    {
      new string[] {  "0","100","200","300","400","500","600","700","800","900", },
      new string[] {  "1","101","201","301","401","501","601","701","801","901", },
      new string[] {  "2","102","202","302","402","502","602","702","802","902", },
      new string[] {  "3","103","203","303","403","503","603","703","803","903", },
      new string[] {  "4","104","204","304","404","504","604","704","804","904", },
      new string[] {  "5","105","205","305","405","505","605","705","805","905", },
      new string[] {  "6","106","206","306","406","506","606","706","806","906", },
      new string[] {  "7","107","207","307","407","507","607","707","807","907", },
      new string[] {  "8","108","208","308","408","508","608","708","808","908", },
      new string[] {  "9","109","209","309","409","509","609","709","809","909", },
      new string[] { "10","110","210","310","410","510","610","710","810","910", },
      new string[] { "11","111","211","311","411","511","611","711","811","911", },
      new string[] { "12","112","212","312","412","512","612","712","812","912", },
      new string[] { "13","113","213","313","413","513","613","713","813","913", },
      new string[] { "14","114","214","314","414","514","614","714","814","914", },
      new string[] { "15","115","215","315","415","515","615","715","815","915", },
      new string[] { "16","116","216","316","416","516","616","716","816","916", },
      new string[] { "17","117","217","317","417","517","617","717","817","917", },
      new string[] { "18","118","218","318","418","518","618","718","818","918", },
      new string[] { "19","119","219","319","419","519","619","719","819","919", },
      new string[] { "20","120","220","320","420","520","620","720","820","920", },
      new string[] { "21","121","221","321","421","521","621","721","821","921", },
      new string[] { "22","122","222","322","422","522","622","722","822","922", },
      new string[] { "23","123","223","323","423","523","623","723","823","923", },
      new string[] { "24","124","224","324","424","524","624","724","824","924", },
      new string[] { "25","125","225","325","425","525","625","725","825","925", },
      new string[] { "26","126","226","326","426","526","626","726","826","926", },
      new string[] { "27","127","227","327","427","527","627","727","827","927", },
      new string[] { "28","128","228","328","428","528","628","728","828","928", },
      new string[] { "29","129","229","329","429","529","629","729","829","929", },
      new string[] { "30","130","230","330","430","530","630","730","830","930", },
      new string[] { "31","131","231","331","431","531","631","731","831","931", },
      new string[] { "32","132","232","332","432","532","632","732","832","932", },
      new string[] { "33","133","233","333","433","533","633","733","833","933", },
      new string[] { "34","134","234","334","434","534","634","734","834","934", },
      new string[] { "35","135","235","335","435","535","635","735","835","935", },
      new string[] { "36","136","236","336","436","536","636","736","836","936", },
      new string[] { "37","137","237","337","437","537","637","737","837","937", },
      new string[] { "38","138","238","338","438","538","638","738","838","938", },
      new string[] { "39","139","239","339","439","539","639","739","839","939", },
      new string[] { "40","140","240","340","440","540","640","740","840","940", },
      new string[] { "41","141","241","341","441","541","641","741","841","941", },
      new string[] { "42","142","242","342","442","542","642","742","842","942", },
      new string[] { "43","143","243","343","443","543","643","743","843","943", },
      new string[] { "44","144","244","344","444","544","644","744","844","944", },
      new string[] { "45","145","245","345","445","545","645","745","845","945", },
      new string[] { "46","146","246","346","446","546","646","746","846","946", },
      new string[] { "47","147","247","347","447","547","647","747","847","947", },
      new string[] { "48","148","248","348","448","548","648","748","848","948", },
      new string[] { "49","149","249","349","449","549","649","749","849","949", },
      new string[] { "50","150","250","350","450","550","650","750","850","950", },
      new string[] { "51","151","251","351","451","551","651","751","851","951", },
      new string[] { "52","152","252","352","452","552","652","752","852","952", },
      new string[] { "53","153","253","353","453","553","653","753","853","953", },
      new string[] { "54","154","254","354","454","554","654","754","854","954", },
      new string[] { "55","155","255","355","455","555","655","755","855","955", },
      new string[] { "56","156","256","356","456","556","656","756","856","956", },
      new string[] { "57","157","257","357","457","557","657","757","857","957", },
      new string[] { "58","158","258","358","458","558","658","758","858","958", },
      new string[] { "59","159","259","359","459","559","659","759","859","959", },
      new string[] { "60","160","260","360","460","560","660","760","860","960", },
      new string[] { "61","161","261","361","461","561","661","761","861","961", },
      new string[] { "62","162","262","362","462","562","662","762","862","962", },
      new string[] { "63","163","263","363","463","563","663","763","863","963", },
      new string[] { "64","164","264","364","464","564","664","764","864","964", },
      new string[] { "65","165","265","365","465","565","665","765","865","965", },
      new string[] { "66","166","266","366","466","566","666","766","866","966", },
      new string[] { "67","167","267","367","467","567","667","767","867","967", },
      new string[] { "68","168","268","368","468","568","668","768","868","968", },
      new string[] { "69","169","269","369","469","569","669","769","869","969", },
      new string[] { "70","170","270","370","470","570","670","770","870","970", },
      new string[] { "71","171","271","371","471","571","671","771","871","971", },
      new string[] { "72","172","272","372","472","572","672","772","872","972", },
      new string[] { "73","173","273","373","473","573","673","773","873","973", },
      new string[] { "74","174","274","374","474","574","674","774","874","974", },
      new string[] { "75","175","275","375","475","575","675","775","875","975", },
      new string[] { "76","176","276","376","476","576","676","776","876","976", },
      new string[] { "77","177","277","377","477","577","677","777","877","977", },
      new string[] { "78","178","278","378","478","578","678","778","878","978", },
      new string[] { "79","179","279","379","479","579","679","779","879","979", },
      new string[] { "80","180","280","380","480","580","680","780","880","980", },
      new string[] { "81","181","281","381","481","581","681","781","881","981", },
      new string[] { "82","182","282","382","482","582","682","782","882","982", },
      new string[] { "83","183","283","383","483","583","683","783","883","983", },
      new string[] { "84","184","284","384","484","584","684","784","884","984", },
      new string[] { "85","185","285","385","485","585","685","785","885","985", },
      new string[] { "86","186","286","386","486","586","686","786","886","986", },
      new string[] { "87","187","287","387","487","587","687","787","887","987", },
      new string[] { "88","188","288","388","488","588","688","788","888","988", },
      new string[] { "89","189","289","389","489","589","689","789","889","989", },
      new string[] { "90","190","290","390","490","590","690","790","890","990", },
      new string[] { "91","191","291","391","491","591","691","791","891","991", },
      new string[] { "92","192","292","392","492","592","692","792","892","992", },
      new string[] { "93","193","293","393","493","593","693","793","893","993", },
      new string[] { "94","194","294","394","494","594","694","794","894","994", },
      new string[] { "95","195","295","395","495","595","695","795","895","995", },
      new string[] { "96","196","296","396","496","596","696","796","896","996", },
      new string[] { "97","197","297","397","497","597","697","797","897","997", },
      new string[] { "98","198","298","398","498","598","698","798","898","998", },
      new string[] { "99","199","299","399","499","599","699","799","899","999", },
    };

    private static readonly string[][] LOOKUP_TABLE_0ms =
    {
      new string[] {  "0ms","100ms","200ms","300ms","400ms","500ms","600ms","700ms","800ms","900ms", },
      new string[] {  "1ms","101ms","201ms","301ms","401ms","501ms","601ms","701ms","801ms","901ms", },
      new string[] {  "2ms","102ms","202ms","302ms","402ms","502ms","602ms","702ms","802ms","902ms", },
      new string[] {  "3ms","103ms","203ms","303ms","403ms","503ms","603ms","703ms","803ms","903ms", },
      new string[] {  "4ms","104ms","204ms","304ms","404ms","504ms","604ms","704ms","804ms","904ms", },
      new string[] {  "5ms","105ms","205ms","305ms","405ms","505ms","605ms","705ms","805ms","905ms", },
      new string[] {  "6ms","106ms","206ms","306ms","406ms","506ms","606ms","706ms","806ms","906ms", },
      new string[] {  "7ms","107ms","207ms","307ms","407ms","507ms","607ms","707ms","807ms","907ms", },
      new string[] {  "8ms","108ms","208ms","308ms","408ms","508ms","608ms","708ms","808ms","908ms", },
      new string[] {  "9ms","109ms","209ms","309ms","409ms","509ms","609ms","709ms","809ms","909ms", },
      new string[] { "10ms","110ms","210ms","310ms","410ms","510ms","610ms","710ms","810ms","910ms", },
      new string[] { "11ms","111ms","211ms","311ms","411ms","511ms","611ms","711ms","811ms","911ms", },
      new string[] { "12ms","112ms","212ms","312ms","412ms","512ms","612ms","712ms","812ms","912ms", },
      new string[] { "13ms","113ms","213ms","313ms","413ms","513ms","613ms","713ms","813ms","913ms", },
      new string[] { "14ms","114ms","214ms","314ms","414ms","514ms","614ms","714ms","814ms","914ms", },
      new string[] { "15ms","115ms","215ms","315ms","415ms","515ms","615ms","715ms","815ms","915ms", },
      new string[] { "16ms","116ms","216ms","316ms","416ms","516ms","616ms","716ms","816ms","916ms", },
      new string[] { "17ms","117ms","217ms","317ms","417ms","517ms","617ms","717ms","817ms","917ms", },
      new string[] { "18ms","118ms","218ms","318ms","418ms","518ms","618ms","718ms","818ms","918ms", },
      new string[] { "19ms","119ms","219ms","319ms","419ms","519ms","619ms","719ms","819ms","919ms", },
      new string[] { "20ms","120ms","220ms","320ms","420ms","520ms","620ms","720ms","820ms","920ms", },
      new string[] { "21ms","121ms","221ms","321ms","421ms","521ms","621ms","721ms","821ms","921ms", },
      new string[] { "22ms","122ms","222ms","322ms","422ms","522ms","622ms","722ms","822ms","922ms", },
      new string[] { "23ms","123ms","223ms","323ms","423ms","523ms","623ms","723ms","823ms","923ms", },
      new string[] { "24ms","124ms","224ms","324ms","424ms","524ms","624ms","724ms","824ms","924ms", },
      new string[] { "25ms","125ms","225ms","325ms","425ms","525ms","625ms","725ms","825ms","925ms", },
      new string[] { "26ms","126ms","226ms","326ms","426ms","526ms","626ms","726ms","826ms","926ms", },
      new string[] { "27ms","127ms","227ms","327ms","427ms","527ms","627ms","727ms","827ms","927ms", },
      new string[] { "28ms","128ms","228ms","328ms","428ms","528ms","628ms","728ms","828ms","928ms", },
      new string[] { "29ms","129ms","229ms","329ms","429ms","529ms","629ms","729ms","829ms","929ms", },
      new string[] { "30ms","130ms","230ms","330ms","430ms","530ms","630ms","730ms","830ms","930ms", },
      new string[] { "31ms","131ms","231ms","331ms","431ms","531ms","631ms","731ms","831ms","931ms", },
      new string[] { "32ms","132ms","232ms","332ms","432ms","532ms","632ms","732ms","832ms","932ms", },
      new string[] { "33ms","133ms","233ms","333ms","433ms","533ms","633ms","733ms","833ms","933ms", },
      new string[] { "34ms","134ms","234ms","334ms","434ms","534ms","634ms","734ms","834ms","934ms", },
      new string[] { "35ms","135ms","235ms","335ms","435ms","535ms","635ms","735ms","835ms","935ms", },
      new string[] { "36ms","136ms","236ms","336ms","436ms","536ms","636ms","736ms","836ms","936ms", },
      new string[] { "37ms","137ms","237ms","337ms","437ms","537ms","637ms","737ms","837ms","937ms", },
      new string[] { "38ms","138ms","238ms","338ms","438ms","538ms","638ms","738ms","838ms","938ms", },
      new string[] { "39ms","139ms","239ms","339ms","439ms","539ms","639ms","739ms","839ms","939ms", },
      new string[] { "40ms","140ms","240ms","340ms","440ms","540ms","640ms","740ms","840ms","940ms", },
      new string[] { "41ms","141ms","241ms","341ms","441ms","541ms","641ms","741ms","841ms","941ms", },
      new string[] { "42ms","142ms","242ms","342ms","442ms","542ms","642ms","742ms","842ms","942ms", },
      new string[] { "43ms","143ms","243ms","343ms","443ms","543ms","643ms","743ms","843ms","943ms", },
      new string[] { "44ms","144ms","244ms","344ms","444ms","544ms","644ms","744ms","844ms","944ms", },
      new string[] { "45ms","145ms","245ms","345ms","445ms","545ms","645ms","745ms","845ms","945ms", },
      new string[] { "46ms","146ms","246ms","346ms","446ms","546ms","646ms","746ms","846ms","946ms", },
      new string[] { "47ms","147ms","247ms","347ms","447ms","547ms","647ms","747ms","847ms","947ms", },
      new string[] { "48ms","148ms","248ms","348ms","448ms","548ms","648ms","748ms","848ms","948ms", },
      new string[] { "49ms","149ms","249ms","349ms","449ms","549ms","649ms","749ms","849ms","949ms", },
      new string[] { "50ms","150ms","250ms","350ms","450ms","550ms","650ms","750ms","850ms","950ms", },
      new string[] { "51ms","151ms","251ms","351ms","451ms","551ms","651ms","751ms","851ms","951ms", },
      new string[] { "52ms","152ms","252ms","352ms","452ms","552ms","652ms","752ms","852ms","952ms", },
      new string[] { "53ms","153ms","253ms","353ms","453ms","553ms","653ms","753ms","853ms","953ms", },
      new string[] { "54ms","154ms","254ms","354ms","454ms","554ms","654ms","754ms","854ms","954ms", },
      new string[] { "55ms","155ms","255ms","355ms","455ms","555ms","655ms","755ms","855ms","955ms", },
      new string[] { "56ms","156ms","256ms","356ms","456ms","556ms","656ms","756ms","856ms","956ms", },
      new string[] { "57ms","157ms","257ms","357ms","457ms","557ms","657ms","757ms","857ms","957ms", },
      new string[] { "58ms","158ms","258ms","358ms","458ms","558ms","658ms","758ms","858ms","958ms", },
      new string[] { "59ms","159ms","259ms","359ms","459ms","559ms","659ms","759ms","859ms","959ms", },
      new string[] { "60ms","160ms","260ms","360ms","460ms","560ms","660ms","760ms","860ms","960ms", },
      new string[] { "61ms","161ms","261ms","361ms","461ms","561ms","661ms","761ms","861ms","961ms", },
      new string[] { "62ms","162ms","262ms","362ms","462ms","562ms","662ms","762ms","862ms","962ms", },
      new string[] { "63ms","163ms","263ms","363ms","463ms","563ms","663ms","763ms","863ms","963ms", },
      new string[] { "64ms","164ms","264ms","364ms","464ms","564ms","664ms","764ms","864ms","964ms", },
      new string[] { "65ms","165ms","265ms","365ms","465ms","565ms","665ms","765ms","865ms","965ms", },
      new string[] { "66ms","166ms","266ms","366ms","466ms","566ms","666ms","766ms","866ms","966ms", },
      new string[] { "67ms","167ms","267ms","367ms","467ms","567ms","667ms","767ms","867ms","967ms", },
      new string[] { "68ms","168ms","268ms","368ms","468ms","568ms","668ms","768ms","868ms","968ms", },
      new string[] { "69ms","169ms","269ms","369ms","469ms","569ms","669ms","769ms","869ms","969ms", },
      new string[] { "70ms","170ms","270ms","370ms","470ms","570ms","670ms","770ms","870ms","970ms", },
      new string[] { "71ms","171ms","271ms","371ms","471ms","571ms","671ms","771ms","871ms","971ms", },
      new string[] { "72ms","172ms","272ms","372ms","472ms","572ms","672ms","772ms","872ms","972ms", },
      new string[] { "73ms","173ms","273ms","373ms","473ms","573ms","673ms","773ms","873ms","973ms", },
      new string[] { "74ms","174ms","274ms","374ms","474ms","574ms","674ms","774ms","874ms","974ms", },
      new string[] { "75ms","175ms","275ms","375ms","475ms","575ms","675ms","775ms","875ms","975ms", },
      new string[] { "76ms","176ms","276ms","376ms","476ms","576ms","676ms","776ms","876ms","976ms", },
      new string[] { "77ms","177ms","277ms","377ms","477ms","577ms","677ms","777ms","877ms","977ms", },
      new string[] { "78ms","178ms","278ms","378ms","478ms","578ms","678ms","778ms","878ms","978ms", },
      new string[] { "79ms","179ms","279ms","379ms","479ms","579ms","679ms","779ms","879ms","979ms", },
      new string[] { "80ms","180ms","280ms","380ms","480ms","580ms","680ms","780ms","880ms","980ms", },
      new string[] { "81ms","181ms","281ms","381ms","481ms","581ms","681ms","781ms","881ms","981ms", },
      new string[] { "82ms","182ms","282ms","382ms","482ms","582ms","682ms","782ms","882ms","982ms", },
      new string[] { "83ms","183ms","283ms","383ms","483ms","583ms","683ms","783ms","883ms","983ms", },
      new string[] { "84ms","184ms","284ms","384ms","484ms","584ms","684ms","784ms","884ms","984ms", },
      new string[] { "85ms","185ms","285ms","385ms","485ms","585ms","685ms","785ms","885ms","985ms", },
      new string[] { "86ms","186ms","286ms","386ms","486ms","586ms","686ms","786ms","886ms","986ms", },
      new string[] { "87ms","187ms","287ms","387ms","487ms","587ms","687ms","787ms","887ms","987ms", },
      new string[] { "88ms","188ms","288ms","388ms","488ms","588ms","688ms","788ms","888ms","988ms", },
      new string[] { "89ms","189ms","289ms","389ms","489ms","589ms","689ms","789ms","889ms","989ms", },
      new string[] { "90ms","190ms","290ms","390ms","490ms","590ms","690ms","790ms","890ms","990ms", },
      new string[] { "91ms","191ms","291ms","391ms","491ms","591ms","691ms","791ms","891ms","991ms", },
      new string[] { "92ms","192ms","292ms","392ms","492ms","592ms","692ms","792ms","892ms","992ms", },
      new string[] { "93ms","193ms","293ms","393ms","493ms","593ms","693ms","793ms","893ms","993ms", },
      new string[] { "94ms","194ms","294ms","394ms","494ms","594ms","694ms","794ms","894ms","994ms", },
      new string[] { "95ms","195ms","295ms","395ms","495ms","595ms","695ms","795ms","895ms","995ms", },
      new string[] { "96ms","196ms","296ms","396ms","496ms","596ms","696ms","796ms","896ms","996ms", },
      new string[] { "97ms","197ms","297ms","397ms","497ms","597ms","697ms","797ms","897ms","997ms", },
      new string[] { "98ms","198ms","298ms","398ms","498ms","598ms","698ms","798ms","898ms","998ms", },
      new string[] { "99ms","199ms","299ms","399ms","499ms","599ms","699ms","799ms","899ms","999ms", },
    };
    
    private static readonly string[][] LOOKUP_TABLE_0_BYTES =
    {
      new string[] {  "0 B","100 B","200 B","300 B","400 B","500 B","600 B","700 B","800 B","900 B", },
      new string[] {  "1 B","101 B","201 B","301 B","401 B","501 B","601 B","701 B","801 B","901 B", },
      new string[] {  "2 B","102 B","202 B","302 B","402 B","502 B","602 B","702 B","802 B","902 B", },
      new string[] {  "3 B","103 B","203 B","303 B","403 B","503 B","603 B","703 B","803 B","903 B", },
      new string[] {  "4 B","104 B","204 B","304 B","404 B","504 B","604 B","704 B","804 B","904 B", },
      new string[] {  "5 B","105 B","205 B","305 B","405 B","505 B","605 B","705 B","805 B","905 B", },
      new string[] {  "6 B","106 B","206 B","306 B","406 B","506 B","606 B","706 B","806 B","906 B", },
      new string[] {  "7 B","107 B","207 B","307 B","407 B","507 B","607 B","707 B","807 B","907 B", },
      new string[] {  "8 B","108 B","208 B","308 B","408 B","508 B","608 B","708 B","808 B","908 B", },
      new string[] {  "9 B","109 B","209 B","309 B","409 B","509 B","609 B","709 B","809 B","909 B", },
      new string[] { "10 B","110 B","210 B","310 B","410 B","510 B","610 B","710 B","810 B","910 B", },
      new string[] { "11 B","111 B","211 B","311 B","411 B","511 B","611 B","711 B","811 B","911 B", },
      new string[] { "12 B","112 B","212 B","312 B","412 B","512 B","612 B","712 B","812 B","912 B", },
      new string[] { "13 B","113 B","213 B","313 B","413 B","513 B","613 B","713 B","813 B","913 B", },
      new string[] { "14 B","114 B","214 B","314 B","414 B","514 B","614 B","714 B","814 B","914 B", },
      new string[] { "15 B","115 B","215 B","315 B","415 B","515 B","615 B","715 B","815 B","915 B", },
      new string[] { "16 B","116 B","216 B","316 B","416 B","516 B","616 B","716 B","816 B","916 B", },
      new string[] { "17 B","117 B","217 B","317 B","417 B","517 B","617 B","717 B","817 B","917 B", },
      new string[] { "18 B","118 B","218 B","318 B","418 B","518 B","618 B","718 B","818 B","918 B", },
      new string[] { "19 B","119 B","219 B","319 B","419 B","519 B","619 B","719 B","819 B","919 B", },
      new string[] { "20 B","120 B","220 B","320 B","420 B","520 B","620 B","720 B","820 B","920 B", },
      new string[] { "21 B","121 B","221 B","321 B","421 B","521 B","621 B","721 B","821 B","921 B", },
      new string[] { "22 B","122 B","222 B","322 B","422 B","522 B","622 B","722 B","822 B","922 B", },
      new string[] { "23 B","123 B","223 B","323 B","423 B","523 B","623 B","723 B","823 B","923 B", },
      new string[] { "24 B","124 B","224 B","324 B","424 B","524 B","624 B","724 B","824 B","924 B", },
      new string[] { "25 B","125 B","225 B","325 B","425 B","525 B","625 B","725 B","825 B","925 B", },
      new string[] { "26 B","126 B","226 B","326 B","426 B","526 B","626 B","726 B","826 B","926 B", },
      new string[] { "27 B","127 B","227 B","327 B","427 B","527 B","627 B","727 B","827 B","927 B", },
      new string[] { "28 B","128 B","228 B","328 B","428 B","528 B","628 B","728 B","828 B","928 B", },
      new string[] { "29 B","129 B","229 B","329 B","429 B","529 B","629 B","729 B","829 B","929 B", },
      new string[] { "30 B","130 B","230 B","330 B","430 B","530 B","630 B","730 B","830 B","930 B", },
      new string[] { "31 B","131 B","231 B","331 B","431 B","531 B","631 B","731 B","831 B","931 B", },
      new string[] { "32 B","132 B","232 B","332 B","432 B","532 B","632 B","732 B","832 B","932 B", },
      new string[] { "33 B","133 B","233 B","333 B","433 B","533 B","633 B","733 B","833 B","933 B", },
      new string[] { "34 B","134 B","234 B","334 B","434 B","534 B","634 B","734 B","834 B","934 B", },
      new string[] { "35 B","135 B","235 B","335 B","435 B","535 B","635 B","735 B","835 B","935 B", },
      new string[] { "36 B","136 B","236 B","336 B","436 B","536 B","636 B","736 B","836 B","936 B", },
      new string[] { "37 B","137 B","237 B","337 B","437 B","537 B","637 B","737 B","837 B","937 B", },
      new string[] { "38 B","138 B","238 B","338 B","438 B","538 B","638 B","738 B","838 B","938 B", },
      new string[] { "39 B","139 B","239 B","339 B","439 B","539 B","639 B","739 B","839 B","939 B", },
      new string[] { "40 B","140 B","240 B","340 B","440 B","540 B","640 B","740 B","840 B","940 B", },
      new string[] { "41 B","141 B","241 B","341 B","441 B","541 B","641 B","741 B","841 B","941 B", },
      new string[] { "42 B","142 B","242 B","342 B","442 B","542 B","642 B","742 B","842 B","942 B", },
      new string[] { "43 B","143 B","243 B","343 B","443 B","543 B","643 B","743 B","843 B","943 B", },
      new string[] { "44 B","144 B","244 B","344 B","444 B","544 B","644 B","744 B","844 B","944 B", },
      new string[] { "45 B","145 B","245 B","345 B","445 B","545 B","645 B","745 B","845 B","945 B", },
      new string[] { "46 B","146 B","246 B","346 B","446 B","546 B","646 B","746 B","846 B","946 B", },
      new string[] { "47 B","147 B","247 B","347 B","447 B","547 B","647 B","747 B","847 B","947 B", },
      new string[] { "48 B","148 B","248 B","348 B","448 B","548 B","648 B","748 B","848 B","948 B", },
      new string[] { "49 B","149 B","249 B","349 B","449 B","549 B","649 B","749 B","849 B","949 B", },
      new string[] { "50 B","150 B","250 B","350 B","450 B","550 B","650 B","750 B","850 B","950 B", },
      new string[] { "51 B","151 B","251 B","351 B","451 B","551 B","651 B","751 B","851 B","951 B", },
      new string[] { "52 B","152 B","252 B","352 B","452 B","552 B","652 B","752 B","852 B","952 B", },
      new string[] { "53 B","153 B","253 B","353 B","453 B","553 B","653 B","753 B","853 B","953 B", },
      new string[] { "54 B","154 B","254 B","354 B","454 B","554 B","654 B","754 B","854 B","954 B", },
      new string[] { "55 B","155 B","255 B","355 B","455 B","555 B","655 B","755 B","855 B","955 B", },
      new string[] { "56 B","156 B","256 B","356 B","456 B","556 B","656 B","756 B","856 B","956 B", },
      new string[] { "57 B","157 B","257 B","357 B","457 B","557 B","657 B","757 B","857 B","957 B", },
      new string[] { "58 B","158 B","258 B","358 B","458 B","558 B","658 B","758 B","858 B","958 B", },
      new string[] { "59 B","159 B","259 B","359 B","459 B","559 B","659 B","759 B","859 B","959 B", },
      new string[] { "60 B","160 B","260 B","360 B","460 B","560 B","660 B","760 B","860 B","960 B", },
      new string[] { "61 B","161 B","261 B","361 B","461 B","561 B","661 B","761 B","861 B","961 B", },
      new string[] { "62 B","162 B","262 B","362 B","462 B","562 B","662 B","762 B","862 B","962 B", },
      new string[] { "63 B","163 B","263 B","363 B","463 B","563 B","663 B","763 B","863 B","963 B", },
      new string[] { "64 B","164 B","264 B","364 B","464 B","564 B","664 B","764 B","864 B","964 B", },
      new string[] { "65 B","165 B","265 B","365 B","465 B","565 B","665 B","765 B","865 B","965 B", },
      new string[] { "66 B","166 B","266 B","366 B","466 B","566 B","666 B","766 B","866 B","966 B", },
      new string[] { "67 B","167 B","267 B","367 B","467 B","567 B","667 B","767 B","867 B","967 B", },
      new string[] { "68 B","168 B","268 B","368 B","468 B","568 B","668 B","768 B","868 B","968 B", },
      new string[] { "69 B","169 B","269 B","369 B","469 B","569 B","669 B","769 B","869 B","969 B", },
      new string[] { "70 B","170 B","270 B","370 B","470 B","570 B","670 B","770 B","870 B","970 B", },
      new string[] { "71 B","171 B","271 B","371 B","471 B","571 B","671 B","771 B","871 B","971 B", },
      new string[] { "72 B","172 B","272 B","372 B","472 B","572 B","672 B","772 B","872 B","972 B", },
      new string[] { "73 B","173 B","273 B","373 B","473 B","573 B","673 B","773 B","873 B","973 B", },
      new string[] { "74 B","174 B","274 B","374 B","474 B","574 B","674 B","774 B","874 B","974 B", },
      new string[] { "75 B","175 B","275 B","375 B","475 B","575 B","675 B","775 B","875 B","975 B", },
      new string[] { "76 B","176 B","276 B","376 B","476 B","576 B","676 B","776 B","876 B","976 B", },
      new string[] { "77 B","177 B","277 B","377 B","477 B","577 B","677 B","777 B","877 B","977 B", },
      new string[] { "78 B","178 B","278 B","378 B","478 B","578 B","678 B","778 B","878 B","978 B", },
      new string[] { "79 B","179 B","279 B","379 B","479 B","579 B","679 B","779 B","879 B","979 B", },
      new string[] { "80 B","180 B","280 B","380 B","480 B","580 B","680 B","780 B","880 B","980 B", },
      new string[] { "81 B","181 B","281 B","381 B","481 B","581 B","681 B","781 B","881 B","981 B", },
      new string[] { "82 B","182 B","282 B","382 B","482 B","582 B","682 B","782 B","882 B","982 B", },
      new string[] { "83 B","183 B","283 B","383 B","483 B","583 B","683 B","783 B","883 B","983 B", },
      new string[] { "84 B","184 B","284 B","384 B","484 B","584 B","684 B","784 B","884 B","984 B", },
      new string[] { "85 B","185 B","285 B","385 B","485 B","585 B","685 B","785 B","885 B","985 B", },
      new string[] { "86 B","186 B","286 B","386 B","486 B","586 B","686 B","786 B","886 B","986 B", },
      new string[] { "87 B","187 B","287 B","387 B","487 B","587 B","687 B","787 B","887 B","987 B", },
      new string[] { "88 B","188 B","288 B","388 B","488 B","588 B","688 B","788 B","888 B","988 B", },
      new string[] { "89 B","189 B","289 B","389 B","489 B","589 B","689 B","789 B","889 B","989 B", },
      new string[] { "90 B","190 B","290 B","390 B","490 B","590 B","690 B","790 B","890 B","990 B", },
      new string[] { "91 B","191 B","291 B","391 B","491 B","591 B","691 B","791 B","891 B","991 B", },
      new string[] { "92 B","192 B","292 B","392 B","492 B","592 B","692 B","792 B","892 B","992 B", },
      new string[] { "93 B","193 B","293 B","393 B","493 B","593 B","693 B","793 B","893 B","993 B", },
      new string[] { "94 B","194 B","294 B","394 B","494 B","594 B","694 B","794 B","894 B","994 B", },
      new string[] { "95 B","195 B","295 B","395 B","495 B","595 B","695 B","795 B","895 B","995 B", },
      new string[] { "96 B","196 B","296 B","396 B","496 B","596 B","696 B","796 B","896 B","996 B", },
      new string[] { "97 B","197 B","297 B","397 B","497 B","597 B","697 B","797 B","897 B","997 B", },
      new string[] { "98 B","198 B","298 B","398 B","498 B","598 B","698 B","798 B","898 B","998 B", },
      new string[] { "99 B","199 B","299 B","399 B","499 B","599 B","699 B","799 B","899 B","999 B", },
    };

    private static readonly string[][] LOOKUP_TABLE_0_00ms =
    {
      new string[] { "0.00ms","1.00ms","2.00ms","3.00ms","4.00ms","5.00ms","6.00ms","7.00ms","8.00ms","9.00ms",    "10.00ms","11.00ms","12.00ms","13.00ms","14.00ms","15.00ms","16.00ms","17.00ms","18.00ms","19.00ms",    "20.00ms","21.00ms","22.00ms","23.00ms","24.00ms","25.00ms","26.00ms","27.00ms","28.00ms","29.00ms",    "30.00ms","31.00ms","32.00ms","33.00ms","34.00ms","35.00ms","36.00ms","37.00ms","38.00ms","39.00ms",    "40.00ms","41.00ms","42.00ms","43.00ms","44.00ms","45.00ms","46.00ms","47.00ms","48.00ms","49.00ms",    "50.00ms","51.00ms","52.00ms","53.00ms","54.00ms","55.00ms","56.00ms","57.00ms","58.00ms","59.00ms",    "60.00ms","61.00ms","62.00ms","63.00ms","64.00ms","65.00ms","66.00ms","67.00ms","68.00ms","69.00ms",    "70.00ms","71.00ms","72.00ms","73.00ms","74.00ms","75.00ms","76.00ms","77.00ms","78.00ms","79.00ms",    "80.00ms","81.00ms","82.00ms","83.00ms","84.00ms","85.00ms","86.00ms","87.00ms","88.00ms","89.00ms",    "90.00ms","91.00ms","92.00ms","93.00ms","94.00ms","95.00ms","96.00ms","97.00ms","98.00ms","99.00ms", },
      new string[] { "0.01ms","1.01ms","2.01ms","3.01ms","4.01ms","5.01ms","6.01ms","7.01ms","8.01ms","9.01ms",    "10.01ms","11.01ms","12.01ms","13.01ms","14.01ms","15.01ms","16.01ms","17.01ms","18.01ms","19.01ms",    "20.01ms","21.01ms","22.01ms","23.01ms","24.01ms","25.01ms","26.01ms","27.01ms","28.01ms","29.01ms",    "30.01ms","31.01ms","32.01ms","33.01ms","34.01ms","35.01ms","36.01ms","37.01ms","38.01ms","39.01ms",    "40.01ms","41.01ms","42.01ms","43.01ms","44.01ms","45.01ms","46.01ms","47.01ms","48.01ms","49.01ms",    "50.01ms","51.01ms","52.01ms","53.01ms","54.01ms","55.01ms","56.01ms","57.01ms","58.01ms","59.01ms",    "60.01ms","61.01ms","62.01ms","63.01ms","64.01ms","65.01ms","66.01ms","67.01ms","68.01ms","69.01ms",    "70.01ms","71.01ms","72.01ms","73.01ms","74.01ms","75.01ms","76.01ms","77.01ms","78.01ms","79.01ms",    "80.01ms","81.01ms","82.01ms","83.01ms","84.01ms","85.01ms","86.01ms","87.01ms","88.01ms","89.01ms",    "90.01ms","91.01ms","92.01ms","93.01ms","94.01ms","95.01ms","96.01ms","97.01ms","98.01ms","99.01ms", },
      new string[] { "0.02ms","1.02ms","2.02ms","3.02ms","4.02ms","5.02ms","6.02ms","7.02ms","8.02ms","9.02ms",    "10.02ms","11.02ms","12.02ms","13.02ms","14.02ms","15.02ms","16.02ms","17.02ms","18.02ms","19.02ms",    "20.02ms","21.02ms","22.02ms","23.02ms","24.02ms","25.02ms","26.02ms","27.02ms","28.02ms","29.02ms",    "30.02ms","31.02ms","32.02ms","33.02ms","34.02ms","35.02ms","36.02ms","37.02ms","38.02ms","39.02ms",    "40.02ms","41.02ms","42.02ms","43.02ms","44.02ms","45.02ms","46.02ms","47.02ms","48.02ms","49.02ms",    "50.02ms","51.02ms","52.02ms","53.02ms","54.02ms","55.02ms","56.02ms","57.02ms","58.02ms","59.02ms",    "60.02ms","61.02ms","62.02ms","63.02ms","64.02ms","65.02ms","66.02ms","67.02ms","68.02ms","69.02ms",    "70.02ms","71.02ms","72.02ms","73.02ms","74.02ms","75.02ms","76.02ms","77.02ms","78.02ms","79.02ms",    "80.02ms","81.02ms","82.02ms","83.02ms","84.02ms","85.02ms","86.02ms","87.02ms","88.02ms","89.02ms",    "90.02ms","91.02ms","92.02ms","93.02ms","94.02ms","95.02ms","96.02ms","97.02ms","98.02ms","99.02ms", },
      new string[] { "0.03ms","1.03ms","2.03ms","3.03ms","4.03ms","5.03ms","6.03ms","7.03ms","8.03ms","9.03ms",    "10.03ms","11.03ms","12.03ms","13.03ms","14.03ms","15.03ms","16.03ms","17.03ms","18.03ms","19.03ms",    "20.03ms","21.03ms","22.03ms","23.03ms","24.03ms","25.03ms","26.03ms","27.03ms","28.03ms","29.03ms",    "30.03ms","31.03ms","32.03ms","33.03ms","34.03ms","35.03ms","36.03ms","37.03ms","38.03ms","39.03ms",    "40.03ms","41.03ms","42.03ms","43.03ms","44.03ms","45.03ms","46.03ms","47.03ms","48.03ms","49.03ms",    "50.03ms","51.03ms","52.03ms","53.03ms","54.03ms","55.03ms","56.03ms","57.03ms","58.03ms","59.03ms",    "60.03ms","61.03ms","62.03ms","63.03ms","64.03ms","65.03ms","66.03ms","67.03ms","68.03ms","69.03ms",    "70.03ms","71.03ms","72.03ms","73.03ms","74.03ms","75.03ms","76.03ms","77.03ms","78.03ms","79.03ms",    "80.03ms","81.03ms","82.03ms","83.03ms","84.03ms","85.03ms","86.03ms","87.03ms","88.03ms","89.03ms",    "90.03ms","91.03ms","92.03ms","93.03ms","94.03ms","95.03ms","96.03ms","97.03ms","98.03ms","99.03ms", },
      new string[] { "0.04ms","1.04ms","2.04ms","3.04ms","4.04ms","5.04ms","6.04ms","7.04ms","8.04ms","9.04ms",    "10.04ms","11.04ms","12.04ms","13.04ms","14.04ms","15.04ms","16.04ms","17.04ms","18.04ms","19.04ms",    "20.04ms","21.04ms","22.04ms","23.04ms","24.04ms","25.04ms","26.04ms","27.04ms","28.04ms","29.04ms",    "30.04ms","31.04ms","32.04ms","33.04ms","34.04ms","35.04ms","36.04ms","37.04ms","38.04ms","39.04ms",    "40.04ms","41.04ms","42.04ms","43.04ms","44.04ms","45.04ms","46.04ms","47.04ms","48.04ms","49.04ms",    "50.04ms","51.04ms","52.04ms","53.04ms","54.04ms","55.04ms","56.04ms","57.04ms","58.04ms","59.04ms",    "60.04ms","61.04ms","62.04ms","63.04ms","64.04ms","65.04ms","66.04ms","67.04ms","68.04ms","69.04ms",    "70.04ms","71.04ms","72.04ms","73.04ms","74.04ms","75.04ms","76.04ms","77.04ms","78.04ms","79.04ms",    "80.04ms","81.04ms","82.04ms","83.04ms","84.04ms","85.04ms","86.04ms","87.04ms","88.04ms","89.04ms",    "90.04ms","91.04ms","92.04ms","93.04ms","94.04ms","95.04ms","96.04ms","97.04ms","98.04ms","99.04ms", },
      new string[] { "0.05ms","1.05ms","2.05ms","3.05ms","4.05ms","5.05ms","6.05ms","7.05ms","8.05ms","9.05ms",    "10.05ms","11.05ms","12.05ms","13.05ms","14.05ms","15.05ms","16.05ms","17.05ms","18.05ms","19.05ms",    "20.05ms","21.05ms","22.05ms","23.05ms","24.05ms","25.05ms","26.05ms","27.05ms","28.05ms","29.05ms",    "30.05ms","31.05ms","32.05ms","33.05ms","34.05ms","35.05ms","36.05ms","37.05ms","38.05ms","39.05ms",    "40.05ms","41.05ms","42.05ms","43.05ms","44.05ms","45.05ms","46.05ms","47.05ms","48.05ms","49.05ms",    "50.05ms","51.05ms","52.05ms","53.05ms","54.05ms","55.05ms","56.05ms","57.05ms","58.05ms","59.05ms",    "60.05ms","61.05ms","62.05ms","63.05ms","64.05ms","65.05ms","66.05ms","67.05ms","68.05ms","69.05ms",    "70.05ms","71.05ms","72.05ms","73.05ms","74.05ms","75.05ms","76.05ms","77.05ms","78.05ms","79.05ms",    "80.05ms","81.05ms","82.05ms","83.05ms","84.05ms","85.05ms","86.05ms","87.05ms","88.05ms","89.05ms",    "90.05ms","91.05ms","92.05ms","93.05ms","94.05ms","95.05ms","96.05ms","97.05ms","98.05ms","99.05ms", },
      new string[] { "0.06ms","1.06ms","2.06ms","3.06ms","4.06ms","5.06ms","6.06ms","7.06ms","8.06ms","9.06ms",    "10.06ms","11.06ms","12.06ms","13.06ms","14.06ms","15.06ms","16.06ms","17.06ms","18.06ms","19.06ms",    "20.06ms","21.06ms","22.06ms","23.06ms","24.06ms","25.06ms","26.06ms","27.06ms","28.06ms","29.06ms",    "30.06ms","31.06ms","32.06ms","33.06ms","34.06ms","35.06ms","36.06ms","37.06ms","38.06ms","39.06ms",    "40.06ms","41.06ms","42.06ms","43.06ms","44.06ms","45.06ms","46.06ms","47.06ms","48.06ms","49.06ms",    "50.06ms","51.06ms","52.06ms","53.06ms","54.06ms","55.06ms","56.06ms","57.06ms","58.06ms","59.06ms",    "60.06ms","61.06ms","62.06ms","63.06ms","64.06ms","65.06ms","66.06ms","67.06ms","68.06ms","69.06ms",    "70.06ms","71.06ms","72.06ms","73.06ms","74.06ms","75.06ms","76.06ms","77.06ms","78.06ms","79.06ms",    "80.06ms","81.06ms","82.06ms","83.06ms","84.06ms","85.06ms","86.06ms","87.06ms","88.06ms","89.06ms",    "90.06ms","91.06ms","92.06ms","93.06ms","94.06ms","95.06ms","96.06ms","97.06ms","98.06ms","99.06ms", },
      new string[] { "0.07ms","1.07ms","2.07ms","3.07ms","4.07ms","5.07ms","6.07ms","7.07ms","8.07ms","9.07ms",    "10.07ms","11.07ms","12.07ms","13.07ms","14.07ms","15.07ms","16.07ms","17.07ms","18.07ms","19.07ms",    "20.07ms","21.07ms","22.07ms","23.07ms","24.07ms","25.07ms","26.07ms","27.07ms","28.07ms","29.07ms",    "30.07ms","31.07ms","32.07ms","33.07ms","34.07ms","35.07ms","36.07ms","37.07ms","38.07ms","39.07ms",    "40.07ms","41.07ms","42.07ms","43.07ms","44.07ms","45.07ms","46.07ms","47.07ms","48.07ms","49.07ms",    "50.07ms","51.07ms","52.07ms","53.07ms","54.07ms","55.07ms","56.07ms","57.07ms","58.07ms","59.07ms",    "60.07ms","61.07ms","62.07ms","63.07ms","64.07ms","65.07ms","66.07ms","67.07ms","68.07ms","69.07ms",    "70.07ms","71.07ms","72.07ms","73.07ms","74.07ms","75.07ms","76.07ms","77.07ms","78.07ms","79.07ms",    "80.07ms","81.07ms","82.07ms","83.07ms","84.07ms","85.07ms","86.07ms","87.07ms","88.07ms","89.07ms",    "90.07ms","91.07ms","92.07ms","93.07ms","94.07ms","95.07ms","96.07ms","97.07ms","98.07ms","99.07ms", },
      new string[] { "0.08ms","1.08ms","2.08ms","3.08ms","4.08ms","5.08ms","6.08ms","7.08ms","8.08ms","9.08ms",    "10.08ms","11.08ms","12.08ms","13.08ms","14.08ms","15.08ms","16.08ms","17.08ms","18.08ms","19.08ms",    "20.08ms","21.08ms","22.08ms","23.08ms","24.08ms","25.08ms","26.08ms","27.08ms","28.08ms","29.08ms",    "30.08ms","31.08ms","32.08ms","33.08ms","34.08ms","35.08ms","36.08ms","37.08ms","38.08ms","39.08ms",    "40.08ms","41.08ms","42.08ms","43.08ms","44.08ms","45.08ms","46.08ms","47.08ms","48.08ms","49.08ms",    "50.08ms","51.08ms","52.08ms","53.08ms","54.08ms","55.08ms","56.08ms","57.08ms","58.08ms","59.08ms",    "60.08ms","61.08ms","62.08ms","63.08ms","64.08ms","65.08ms","66.08ms","67.08ms","68.08ms","69.08ms",    "70.08ms","71.08ms","72.08ms","73.08ms","74.08ms","75.08ms","76.08ms","77.08ms","78.08ms","79.08ms",    "80.08ms","81.08ms","82.08ms","83.08ms","84.08ms","85.08ms","86.08ms","87.08ms","88.08ms","89.08ms",    "90.08ms","91.08ms","92.08ms","93.08ms","94.08ms","95.08ms","96.08ms","97.08ms","98.08ms","99.08ms", },
      new string[] { "0.09ms","1.09ms","2.09ms","3.09ms","4.09ms","5.09ms","6.09ms","7.09ms","8.09ms","9.09ms",    "10.09ms","11.09ms","12.09ms","13.09ms","14.09ms","15.09ms","16.09ms","17.09ms","18.09ms","19.09ms",    "20.09ms","21.09ms","22.09ms","23.09ms","24.09ms","25.09ms","26.09ms","27.09ms","28.09ms","29.09ms",    "30.09ms","31.09ms","32.09ms","33.09ms","34.09ms","35.09ms","36.09ms","37.09ms","38.09ms","39.09ms",    "40.09ms","41.09ms","42.09ms","43.09ms","44.09ms","45.09ms","46.09ms","47.09ms","48.09ms","49.09ms",    "50.09ms","51.09ms","52.09ms","53.09ms","54.09ms","55.09ms","56.09ms","57.09ms","58.09ms","59.09ms",    "60.09ms","61.09ms","62.09ms","63.09ms","64.09ms","65.09ms","66.09ms","67.09ms","68.09ms","69.09ms",    "70.09ms","71.09ms","72.09ms","73.09ms","74.09ms","75.09ms","76.09ms","77.09ms","78.09ms","79.09ms",    "80.09ms","81.09ms","82.09ms","83.09ms","84.09ms","85.09ms","86.09ms","87.09ms","88.09ms","89.09ms",    "90.09ms","91.09ms","92.09ms","93.09ms","94.09ms","95.09ms","96.09ms","97.09ms","98.09ms","99.09ms", },
      new string[] { "0.10ms","1.10ms","2.10ms","3.10ms","4.10ms","5.10ms","6.10ms","7.10ms","8.10ms","9.10ms",    "10.10ms","11.10ms","12.10ms","13.10ms","14.10ms","15.10ms","16.10ms","17.10ms","18.10ms","19.10ms",    "20.10ms","21.10ms","22.10ms","23.10ms","24.10ms","25.10ms","26.10ms","27.10ms","28.10ms","29.10ms",    "30.10ms","31.10ms","32.10ms","33.10ms","34.10ms","35.10ms","36.10ms","37.10ms","38.10ms","39.10ms",    "40.10ms","41.10ms","42.10ms","43.10ms","44.10ms","45.10ms","46.10ms","47.10ms","48.10ms","49.10ms",    "50.10ms","51.10ms","52.10ms","53.10ms","54.10ms","55.10ms","56.10ms","57.10ms","58.10ms","59.10ms",    "60.10ms","61.10ms","62.10ms","63.10ms","64.10ms","65.10ms","66.10ms","67.10ms","68.10ms","69.10ms",    "70.10ms","71.10ms","72.10ms","73.10ms","74.10ms","75.10ms","76.10ms","77.10ms","78.10ms","79.10ms",    "80.10ms","81.10ms","82.10ms","83.10ms","84.10ms","85.10ms","86.10ms","87.10ms","88.10ms","89.10ms",    "90.10ms","91.10ms","92.10ms","93.10ms","94.10ms","95.10ms","96.10ms","97.10ms","98.10ms","99.10ms", },
      new string[] { "0.11ms","1.11ms","2.11ms","3.11ms","4.11ms","5.11ms","6.11ms","7.11ms","8.11ms","9.11ms",    "10.11ms","11.11ms","12.11ms","13.11ms","14.11ms","15.11ms","16.11ms","17.11ms","18.11ms","19.11ms",    "20.11ms","21.11ms","22.11ms","23.11ms","24.11ms","25.11ms","26.11ms","27.11ms","28.11ms","29.11ms",    "30.11ms","31.11ms","32.11ms","33.11ms","34.11ms","35.11ms","36.11ms","37.11ms","38.11ms","39.11ms",    "40.11ms","41.11ms","42.11ms","43.11ms","44.11ms","45.11ms","46.11ms","47.11ms","48.11ms","49.11ms",    "50.11ms","51.11ms","52.11ms","53.11ms","54.11ms","55.11ms","56.11ms","57.11ms","58.11ms","59.11ms",    "60.11ms","61.11ms","62.11ms","63.11ms","64.11ms","65.11ms","66.11ms","67.11ms","68.11ms","69.11ms",    "70.11ms","71.11ms","72.11ms","73.11ms","74.11ms","75.11ms","76.11ms","77.11ms","78.11ms","79.11ms",    "80.11ms","81.11ms","82.11ms","83.11ms","84.11ms","85.11ms","86.11ms","87.11ms","88.11ms","89.11ms",    "90.11ms","91.11ms","92.11ms","93.11ms","94.11ms","95.11ms","96.11ms","97.11ms","98.11ms","99.11ms", },
      new string[] { "0.12ms","1.12ms","2.12ms","3.12ms","4.12ms","5.12ms","6.12ms","7.12ms","8.12ms","9.12ms",    "10.12ms","11.12ms","12.12ms","13.12ms","14.12ms","15.12ms","16.12ms","17.12ms","18.12ms","19.12ms",    "20.12ms","21.12ms","22.12ms","23.12ms","24.12ms","25.12ms","26.12ms","27.12ms","28.12ms","29.12ms",    "30.12ms","31.12ms","32.12ms","33.12ms","34.12ms","35.12ms","36.12ms","37.12ms","38.12ms","39.12ms",    "40.12ms","41.12ms","42.12ms","43.12ms","44.12ms","45.12ms","46.12ms","47.12ms","48.12ms","49.12ms",    "50.12ms","51.12ms","52.12ms","53.12ms","54.12ms","55.12ms","56.12ms","57.12ms","58.12ms","59.12ms",    "60.12ms","61.12ms","62.12ms","63.12ms","64.12ms","65.12ms","66.12ms","67.12ms","68.12ms","69.12ms",    "70.12ms","71.12ms","72.12ms","73.12ms","74.12ms","75.12ms","76.12ms","77.12ms","78.12ms","79.12ms",    "80.12ms","81.12ms","82.12ms","83.12ms","84.12ms","85.12ms","86.12ms","87.12ms","88.12ms","89.12ms",    "90.12ms","91.12ms","92.12ms","93.12ms","94.12ms","95.12ms","96.12ms","97.12ms","98.12ms","99.12ms", },
      new string[] { "0.13ms","1.13ms","2.13ms","3.13ms","4.13ms","5.13ms","6.13ms","7.13ms","8.13ms","9.13ms",    "10.13ms","11.13ms","12.13ms","13.13ms","14.13ms","15.13ms","16.13ms","17.13ms","18.13ms","19.13ms",    "20.13ms","21.13ms","22.13ms","23.13ms","24.13ms","25.13ms","26.13ms","27.13ms","28.13ms","29.13ms",    "30.13ms","31.13ms","32.13ms","33.13ms","34.13ms","35.13ms","36.13ms","37.13ms","38.13ms","39.13ms",    "40.13ms","41.13ms","42.13ms","43.13ms","44.13ms","45.13ms","46.13ms","47.13ms","48.13ms","49.13ms",    "50.13ms","51.13ms","52.13ms","53.13ms","54.13ms","55.13ms","56.13ms","57.13ms","58.13ms","59.13ms",    "60.13ms","61.13ms","62.13ms","63.13ms","64.13ms","65.13ms","66.13ms","67.13ms","68.13ms","69.13ms",    "70.13ms","71.13ms","72.13ms","73.13ms","74.13ms","75.13ms","76.13ms","77.13ms","78.13ms","79.13ms",    "80.13ms","81.13ms","82.13ms","83.13ms","84.13ms","85.13ms","86.13ms","87.13ms","88.13ms","89.13ms",    "90.13ms","91.13ms","92.13ms","93.13ms","94.13ms","95.13ms","96.13ms","97.13ms","98.13ms","99.13ms", },
      new string[] { "0.14ms","1.14ms","2.14ms","3.14ms","4.14ms","5.14ms","6.14ms","7.14ms","8.14ms","9.14ms",    "10.14ms","11.14ms","12.14ms","13.14ms","14.14ms","15.14ms","16.14ms","17.14ms","18.14ms","19.14ms",    "20.14ms","21.14ms","22.14ms","23.14ms","24.14ms","25.14ms","26.14ms","27.14ms","28.14ms","29.14ms",    "30.14ms","31.14ms","32.14ms","33.14ms","34.14ms","35.14ms","36.14ms","37.14ms","38.14ms","39.14ms",    "40.14ms","41.14ms","42.14ms","43.14ms","44.14ms","45.14ms","46.14ms","47.14ms","48.14ms","49.14ms",    "50.14ms","51.14ms","52.14ms","53.14ms","54.14ms","55.14ms","56.14ms","57.14ms","58.14ms","59.14ms",    "60.14ms","61.14ms","62.14ms","63.14ms","64.14ms","65.14ms","66.14ms","67.14ms","68.14ms","69.14ms",    "70.14ms","71.14ms","72.14ms","73.14ms","74.14ms","75.14ms","76.14ms","77.14ms","78.14ms","79.14ms",    "80.14ms","81.14ms","82.14ms","83.14ms","84.14ms","85.14ms","86.14ms","87.14ms","88.14ms","89.14ms",    "90.14ms","91.14ms","92.14ms","93.14ms","94.14ms","95.14ms","96.14ms","97.14ms","98.14ms","99.14ms", },
      new string[] { "0.15ms","1.15ms","2.15ms","3.15ms","4.15ms","5.15ms","6.15ms","7.15ms","8.15ms","9.15ms",    "10.15ms","11.15ms","12.15ms","13.15ms","14.15ms","15.15ms","16.15ms","17.15ms","18.15ms","19.15ms",    "20.15ms","21.15ms","22.15ms","23.15ms","24.15ms","25.15ms","26.15ms","27.15ms","28.15ms","29.15ms",    "30.15ms","31.15ms","32.15ms","33.15ms","34.15ms","35.15ms","36.15ms","37.15ms","38.15ms","39.15ms",    "40.15ms","41.15ms","42.15ms","43.15ms","44.15ms","45.15ms","46.15ms","47.15ms","48.15ms","49.15ms",    "50.15ms","51.15ms","52.15ms","53.15ms","54.15ms","55.15ms","56.15ms","57.15ms","58.15ms","59.15ms",    "60.15ms","61.15ms","62.15ms","63.15ms","64.15ms","65.15ms","66.15ms","67.15ms","68.15ms","69.15ms",    "70.15ms","71.15ms","72.15ms","73.15ms","74.15ms","75.15ms","76.15ms","77.15ms","78.15ms","79.15ms",    "80.15ms","81.15ms","82.15ms","83.15ms","84.15ms","85.15ms","86.15ms","87.15ms","88.15ms","89.15ms",    "90.15ms","91.15ms","92.15ms","93.15ms","94.15ms","95.15ms","96.15ms","97.15ms","98.15ms","99.15ms", },
      new string[] { "0.16ms","1.16ms","2.16ms","3.16ms","4.16ms","5.16ms","6.16ms","7.16ms","8.16ms","9.16ms",    "10.16ms","11.16ms","12.16ms","13.16ms","14.16ms","15.16ms","16.16ms","17.16ms","18.16ms","19.16ms",    "20.16ms","21.16ms","22.16ms","23.16ms","24.16ms","25.16ms","26.16ms","27.16ms","28.16ms","29.16ms",    "30.16ms","31.16ms","32.16ms","33.16ms","34.16ms","35.16ms","36.16ms","37.16ms","38.16ms","39.16ms",    "40.16ms","41.16ms","42.16ms","43.16ms","44.16ms","45.16ms","46.16ms","47.16ms","48.16ms","49.16ms",    "50.16ms","51.16ms","52.16ms","53.16ms","54.16ms","55.16ms","56.16ms","57.16ms","58.16ms","59.16ms",    "60.16ms","61.16ms","62.16ms","63.16ms","64.16ms","65.16ms","66.16ms","67.16ms","68.16ms","69.16ms",    "70.16ms","71.16ms","72.16ms","73.16ms","74.16ms","75.16ms","76.16ms","77.16ms","78.16ms","79.16ms",    "80.16ms","81.16ms","82.16ms","83.16ms","84.16ms","85.16ms","86.16ms","87.16ms","88.16ms","89.16ms",    "90.16ms","91.16ms","92.16ms","93.16ms","94.16ms","95.16ms","96.16ms","97.16ms","98.16ms","99.16ms", },
      new string[] { "0.17ms","1.17ms","2.17ms","3.17ms","4.17ms","5.17ms","6.17ms","7.17ms","8.17ms","9.17ms",    "10.17ms","11.17ms","12.17ms","13.17ms","14.17ms","15.17ms","16.17ms","17.17ms","18.17ms","19.17ms",    "20.17ms","21.17ms","22.17ms","23.17ms","24.17ms","25.17ms","26.17ms","27.17ms","28.17ms","29.17ms",    "30.17ms","31.17ms","32.17ms","33.17ms","34.17ms","35.17ms","36.17ms","37.17ms","38.17ms","39.17ms",    "40.17ms","41.17ms","42.17ms","43.17ms","44.17ms","45.17ms","46.17ms","47.17ms","48.17ms","49.17ms",    "50.17ms","51.17ms","52.17ms","53.17ms","54.17ms","55.17ms","56.17ms","57.17ms","58.17ms","59.17ms",    "60.17ms","61.17ms","62.17ms","63.17ms","64.17ms","65.17ms","66.17ms","67.17ms","68.17ms","69.17ms",    "70.17ms","71.17ms","72.17ms","73.17ms","74.17ms","75.17ms","76.17ms","77.17ms","78.17ms","79.17ms",    "80.17ms","81.17ms","82.17ms","83.17ms","84.17ms","85.17ms","86.17ms","87.17ms","88.17ms","89.17ms",    "90.17ms","91.17ms","92.17ms","93.17ms","94.17ms","95.17ms","96.17ms","97.17ms","98.17ms","99.17ms", },
      new string[] { "0.18ms","1.18ms","2.18ms","3.18ms","4.18ms","5.18ms","6.18ms","7.18ms","8.18ms","9.18ms",    "10.18ms","11.18ms","12.18ms","13.18ms","14.18ms","15.18ms","16.18ms","17.18ms","18.18ms","19.18ms",    "20.18ms","21.18ms","22.18ms","23.18ms","24.18ms","25.18ms","26.18ms","27.18ms","28.18ms","29.18ms",    "30.18ms","31.18ms","32.18ms","33.18ms","34.18ms","35.18ms","36.18ms","37.18ms","38.18ms","39.18ms",    "40.18ms","41.18ms","42.18ms","43.18ms","44.18ms","45.18ms","46.18ms","47.18ms","48.18ms","49.18ms",    "50.18ms","51.18ms","52.18ms","53.18ms","54.18ms","55.18ms","56.18ms","57.18ms","58.18ms","59.18ms",    "60.18ms","61.18ms","62.18ms","63.18ms","64.18ms","65.18ms","66.18ms","67.18ms","68.18ms","69.18ms",    "70.18ms","71.18ms","72.18ms","73.18ms","74.18ms","75.18ms","76.18ms","77.18ms","78.18ms","79.18ms",    "80.18ms","81.18ms","82.18ms","83.18ms","84.18ms","85.18ms","86.18ms","87.18ms","88.18ms","89.18ms",    "90.18ms","91.18ms","92.18ms","93.18ms","94.18ms","95.18ms","96.18ms","97.18ms","98.18ms","99.18ms", },
      new string[] { "0.19ms","1.19ms","2.19ms","3.19ms","4.19ms","5.19ms","6.19ms","7.19ms","8.19ms","9.19ms",    "10.19ms","11.19ms","12.19ms","13.19ms","14.19ms","15.19ms","16.19ms","17.19ms","18.19ms","19.19ms",    "20.19ms","21.19ms","22.19ms","23.19ms","24.19ms","25.19ms","26.19ms","27.19ms","28.19ms","29.19ms",    "30.19ms","31.19ms","32.19ms","33.19ms","34.19ms","35.19ms","36.19ms","37.19ms","38.19ms","39.19ms",    "40.19ms","41.19ms","42.19ms","43.19ms","44.19ms","45.19ms","46.19ms","47.19ms","48.19ms","49.19ms",    "50.19ms","51.19ms","52.19ms","53.19ms","54.19ms","55.19ms","56.19ms","57.19ms","58.19ms","59.19ms",    "60.19ms","61.19ms","62.19ms","63.19ms","64.19ms","65.19ms","66.19ms","67.19ms","68.19ms","69.19ms",    "70.19ms","71.19ms","72.19ms","73.19ms","74.19ms","75.19ms","76.19ms","77.19ms","78.19ms","79.19ms",    "80.19ms","81.19ms","82.19ms","83.19ms","84.19ms","85.19ms","86.19ms","87.19ms","88.19ms","89.19ms",    "90.19ms","91.19ms","92.19ms","93.19ms","94.19ms","95.19ms","96.19ms","97.19ms","98.19ms","99.19ms", },
      new string[] { "0.20ms","1.20ms","2.20ms","3.20ms","4.20ms","5.20ms","6.20ms","7.20ms","8.20ms","9.20ms",    "10.20ms","11.20ms","12.20ms","13.20ms","14.20ms","15.20ms","16.20ms","17.20ms","18.20ms","19.20ms",    "20.20ms","21.20ms","22.20ms","23.20ms","24.20ms","25.20ms","26.20ms","27.20ms","28.20ms","29.20ms",    "30.20ms","31.20ms","32.20ms","33.20ms","34.20ms","35.20ms","36.20ms","37.20ms","38.20ms","39.20ms",    "40.20ms","41.20ms","42.20ms","43.20ms","44.20ms","45.20ms","46.20ms","47.20ms","48.20ms","49.20ms",    "50.20ms","51.20ms","52.20ms","53.20ms","54.20ms","55.20ms","56.20ms","57.20ms","58.20ms","59.20ms",    "60.20ms","61.20ms","62.20ms","63.20ms","64.20ms","65.20ms","66.20ms","67.20ms","68.20ms","69.20ms",    "70.20ms","71.20ms","72.20ms","73.20ms","74.20ms","75.20ms","76.20ms","77.20ms","78.20ms","79.20ms",    "80.20ms","81.20ms","82.20ms","83.20ms","84.20ms","85.20ms","86.20ms","87.20ms","88.20ms","89.20ms",    "90.20ms","91.20ms","92.20ms","93.20ms","94.20ms","95.20ms","96.20ms","97.20ms","98.20ms","99.20ms", },
      new string[] { "0.21ms","1.21ms","2.21ms","3.21ms","4.21ms","5.21ms","6.21ms","7.21ms","8.21ms","9.21ms",    "10.21ms","11.21ms","12.21ms","13.21ms","14.21ms","15.21ms","16.21ms","17.21ms","18.21ms","19.21ms",    "20.21ms","21.21ms","22.21ms","23.21ms","24.21ms","25.21ms","26.21ms","27.21ms","28.21ms","29.21ms",    "30.21ms","31.21ms","32.21ms","33.21ms","34.21ms","35.21ms","36.21ms","37.21ms","38.21ms","39.21ms",    "40.21ms","41.21ms","42.21ms","43.21ms","44.21ms","45.21ms","46.21ms","47.21ms","48.21ms","49.21ms",    "50.21ms","51.21ms","52.21ms","53.21ms","54.21ms","55.21ms","56.21ms","57.21ms","58.21ms","59.21ms",    "60.21ms","61.21ms","62.21ms","63.21ms","64.21ms","65.21ms","66.21ms","67.21ms","68.21ms","69.21ms",    "70.21ms","71.21ms","72.21ms","73.21ms","74.21ms","75.21ms","76.21ms","77.21ms","78.21ms","79.21ms",    "80.21ms","81.21ms","82.21ms","83.21ms","84.21ms","85.21ms","86.21ms","87.21ms","88.21ms","89.21ms",    "90.21ms","91.21ms","92.21ms","93.21ms","94.21ms","95.21ms","96.21ms","97.21ms","98.21ms","99.21ms", },
      new string[] { "0.22ms","1.22ms","2.22ms","3.22ms","4.22ms","5.22ms","6.22ms","7.22ms","8.22ms","9.22ms",    "10.22ms","11.22ms","12.22ms","13.22ms","14.22ms","15.22ms","16.22ms","17.22ms","18.22ms","19.22ms",    "20.22ms","21.22ms","22.22ms","23.22ms","24.22ms","25.22ms","26.22ms","27.22ms","28.22ms","29.22ms",    "30.22ms","31.22ms","32.22ms","33.22ms","34.22ms","35.22ms","36.22ms","37.22ms","38.22ms","39.22ms",    "40.22ms","41.22ms","42.22ms","43.22ms","44.22ms","45.22ms","46.22ms","47.22ms","48.22ms","49.22ms",    "50.22ms","51.22ms","52.22ms","53.22ms","54.22ms","55.22ms","56.22ms","57.22ms","58.22ms","59.22ms",    "60.22ms","61.22ms","62.22ms","63.22ms","64.22ms","65.22ms","66.22ms","67.22ms","68.22ms","69.22ms",    "70.22ms","71.22ms","72.22ms","73.22ms","74.22ms","75.22ms","76.22ms","77.22ms","78.22ms","79.22ms",    "80.22ms","81.22ms","82.22ms","83.22ms","84.22ms","85.22ms","86.22ms","87.22ms","88.22ms","89.22ms",    "90.22ms","91.22ms","92.22ms","93.22ms","94.22ms","95.22ms","96.22ms","97.22ms","98.22ms","99.22ms", },
      new string[] { "0.23ms","1.23ms","2.23ms","3.23ms","4.23ms","5.23ms","6.23ms","7.23ms","8.23ms","9.23ms",    "10.23ms","11.23ms","12.23ms","13.23ms","14.23ms","15.23ms","16.23ms","17.23ms","18.23ms","19.23ms",    "20.23ms","21.23ms","22.23ms","23.23ms","24.23ms","25.23ms","26.23ms","27.23ms","28.23ms","29.23ms",    "30.23ms","31.23ms","32.23ms","33.23ms","34.23ms","35.23ms","36.23ms","37.23ms","38.23ms","39.23ms",    "40.23ms","41.23ms","42.23ms","43.23ms","44.23ms","45.23ms","46.23ms","47.23ms","48.23ms","49.23ms",    "50.23ms","51.23ms","52.23ms","53.23ms","54.23ms","55.23ms","56.23ms","57.23ms","58.23ms","59.23ms",    "60.23ms","61.23ms","62.23ms","63.23ms","64.23ms","65.23ms","66.23ms","67.23ms","68.23ms","69.23ms",    "70.23ms","71.23ms","72.23ms","73.23ms","74.23ms","75.23ms","76.23ms","77.23ms","78.23ms","79.23ms",    "80.23ms","81.23ms","82.23ms","83.23ms","84.23ms","85.23ms","86.23ms","87.23ms","88.23ms","89.23ms",    "90.23ms","91.23ms","92.23ms","93.23ms","94.23ms","95.23ms","96.23ms","97.23ms","98.23ms","99.23ms", },
      new string[] { "0.24ms","1.24ms","2.24ms","3.24ms","4.24ms","5.24ms","6.24ms","7.24ms","8.24ms","9.24ms",    "10.24ms","11.24ms","12.24ms","13.24ms","14.24ms","15.24ms","16.24ms","17.24ms","18.24ms","19.24ms",    "20.24ms","21.24ms","22.24ms","23.24ms","24.24ms","25.24ms","26.24ms","27.24ms","28.24ms","29.24ms",    "30.24ms","31.24ms","32.24ms","33.24ms","34.24ms","35.24ms","36.24ms","37.24ms","38.24ms","39.24ms",    "40.24ms","41.24ms","42.24ms","43.24ms","44.24ms","45.24ms","46.24ms","47.24ms","48.24ms","49.24ms",    "50.24ms","51.24ms","52.24ms","53.24ms","54.24ms","55.24ms","56.24ms","57.24ms","58.24ms","59.24ms",    "60.24ms","61.24ms","62.24ms","63.24ms","64.24ms","65.24ms","66.24ms","67.24ms","68.24ms","69.24ms",    "70.24ms","71.24ms","72.24ms","73.24ms","74.24ms","75.24ms","76.24ms","77.24ms","78.24ms","79.24ms",    "80.24ms","81.24ms","82.24ms","83.24ms","84.24ms","85.24ms","86.24ms","87.24ms","88.24ms","89.24ms",    "90.24ms","91.24ms","92.24ms","93.24ms","94.24ms","95.24ms","96.24ms","97.24ms","98.24ms","99.24ms", },
      new string[] { "0.25ms","1.25ms","2.25ms","3.25ms","4.25ms","5.25ms","6.25ms","7.25ms","8.25ms","9.25ms",    "10.25ms","11.25ms","12.25ms","13.25ms","14.25ms","15.25ms","16.25ms","17.25ms","18.25ms","19.25ms",    "20.25ms","21.25ms","22.25ms","23.25ms","24.25ms","25.25ms","26.25ms","27.25ms","28.25ms","29.25ms",    "30.25ms","31.25ms","32.25ms","33.25ms","34.25ms","35.25ms","36.25ms","37.25ms","38.25ms","39.25ms",    "40.25ms","41.25ms","42.25ms","43.25ms","44.25ms","45.25ms","46.25ms","47.25ms","48.25ms","49.25ms",    "50.25ms","51.25ms","52.25ms","53.25ms","54.25ms","55.25ms","56.25ms","57.25ms","58.25ms","59.25ms",    "60.25ms","61.25ms","62.25ms","63.25ms","64.25ms","65.25ms","66.25ms","67.25ms","68.25ms","69.25ms",    "70.25ms","71.25ms","72.25ms","73.25ms","74.25ms","75.25ms","76.25ms","77.25ms","78.25ms","79.25ms",    "80.25ms","81.25ms","82.25ms","83.25ms","84.25ms","85.25ms","86.25ms","87.25ms","88.25ms","89.25ms",    "90.25ms","91.25ms","92.25ms","93.25ms","94.25ms","95.25ms","96.25ms","97.25ms","98.25ms","99.25ms", },
      new string[] { "0.26ms","1.26ms","2.26ms","3.26ms","4.26ms","5.26ms","6.26ms","7.26ms","8.26ms","9.26ms",    "10.26ms","11.26ms","12.26ms","13.26ms","14.26ms","15.26ms","16.26ms","17.26ms","18.26ms","19.26ms",    "20.26ms","21.26ms","22.26ms","23.26ms","24.26ms","25.26ms","26.26ms","27.26ms","28.26ms","29.26ms",    "30.26ms","31.26ms","32.26ms","33.26ms","34.26ms","35.26ms","36.26ms","37.26ms","38.26ms","39.26ms",    "40.26ms","41.26ms","42.26ms","43.26ms","44.26ms","45.26ms","46.26ms","47.26ms","48.26ms","49.26ms",    "50.26ms","51.26ms","52.26ms","53.26ms","54.26ms","55.26ms","56.26ms","57.26ms","58.26ms","59.26ms",    "60.26ms","61.26ms","62.26ms","63.26ms","64.26ms","65.26ms","66.26ms","67.26ms","68.26ms","69.26ms",    "70.26ms","71.26ms","72.26ms","73.26ms","74.26ms","75.26ms","76.26ms","77.26ms","78.26ms","79.26ms",    "80.26ms","81.26ms","82.26ms","83.26ms","84.26ms","85.26ms","86.26ms","87.26ms","88.26ms","89.26ms",    "90.26ms","91.26ms","92.26ms","93.26ms","94.26ms","95.26ms","96.26ms","97.26ms","98.26ms","99.26ms", },
      new string[] { "0.27ms","1.27ms","2.27ms","3.27ms","4.27ms","5.27ms","6.27ms","7.27ms","8.27ms","9.27ms",    "10.27ms","11.27ms","12.27ms","13.27ms","14.27ms","15.27ms","16.27ms","17.27ms","18.27ms","19.27ms",    "20.27ms","21.27ms","22.27ms","23.27ms","24.27ms","25.27ms","26.27ms","27.27ms","28.27ms","29.27ms",    "30.27ms","31.27ms","32.27ms","33.27ms","34.27ms","35.27ms","36.27ms","37.27ms","38.27ms","39.27ms",    "40.27ms","41.27ms","42.27ms","43.27ms","44.27ms","45.27ms","46.27ms","47.27ms","48.27ms","49.27ms",    "50.27ms","51.27ms","52.27ms","53.27ms","54.27ms","55.27ms","56.27ms","57.27ms","58.27ms","59.27ms",    "60.27ms","61.27ms","62.27ms","63.27ms","64.27ms","65.27ms","66.27ms","67.27ms","68.27ms","69.27ms",    "70.27ms","71.27ms","72.27ms","73.27ms","74.27ms","75.27ms","76.27ms","77.27ms","78.27ms","79.27ms",    "80.27ms","81.27ms","82.27ms","83.27ms","84.27ms","85.27ms","86.27ms","87.27ms","88.27ms","89.27ms",    "90.27ms","91.27ms","92.27ms","93.27ms","94.27ms","95.27ms","96.27ms","97.27ms","98.27ms","99.27ms", },
      new string[] { "0.28ms","1.28ms","2.28ms","3.28ms","4.28ms","5.28ms","6.28ms","7.28ms","8.28ms","9.28ms",    "10.28ms","11.28ms","12.28ms","13.28ms","14.28ms","15.28ms","16.28ms","17.28ms","18.28ms","19.28ms",    "20.28ms","21.28ms","22.28ms","23.28ms","24.28ms","25.28ms","26.28ms","27.28ms","28.28ms","29.28ms",    "30.28ms","31.28ms","32.28ms","33.28ms","34.28ms","35.28ms","36.28ms","37.28ms","38.28ms","39.28ms",    "40.28ms","41.28ms","42.28ms","43.28ms","44.28ms","45.28ms","46.28ms","47.28ms","48.28ms","49.28ms",    "50.28ms","51.28ms","52.28ms","53.28ms","54.28ms","55.28ms","56.28ms","57.28ms","58.28ms","59.28ms",    "60.28ms","61.28ms","62.28ms","63.28ms","64.28ms","65.28ms","66.28ms","67.28ms","68.28ms","69.28ms",    "70.28ms","71.28ms","72.28ms","73.28ms","74.28ms","75.28ms","76.28ms","77.28ms","78.28ms","79.28ms",    "80.28ms","81.28ms","82.28ms","83.28ms","84.28ms","85.28ms","86.28ms","87.28ms","88.28ms","89.28ms",    "90.28ms","91.28ms","92.28ms","93.28ms","94.28ms","95.28ms","96.28ms","97.28ms","98.28ms","99.28ms", },
      new string[] { "0.29ms","1.29ms","2.29ms","3.29ms","4.29ms","5.29ms","6.29ms","7.29ms","8.29ms","9.29ms",    "10.29ms","11.29ms","12.29ms","13.29ms","14.29ms","15.29ms","16.29ms","17.29ms","18.29ms","19.29ms",    "20.29ms","21.29ms","22.29ms","23.29ms","24.29ms","25.29ms","26.29ms","27.29ms","28.29ms","29.29ms",    "30.29ms","31.29ms","32.29ms","33.29ms","34.29ms","35.29ms","36.29ms","37.29ms","38.29ms","39.29ms",    "40.29ms","41.29ms","42.29ms","43.29ms","44.29ms","45.29ms","46.29ms","47.29ms","48.29ms","49.29ms",    "50.29ms","51.29ms","52.29ms","53.29ms","54.29ms","55.29ms","56.29ms","57.29ms","58.29ms","59.29ms",    "60.29ms","61.29ms","62.29ms","63.29ms","64.29ms","65.29ms","66.29ms","67.29ms","68.29ms","69.29ms",    "70.29ms","71.29ms","72.29ms","73.29ms","74.29ms","75.29ms","76.29ms","77.29ms","78.29ms","79.29ms",    "80.29ms","81.29ms","82.29ms","83.29ms","84.29ms","85.29ms","86.29ms","87.29ms","88.29ms","89.29ms",    "90.29ms","91.29ms","92.29ms","93.29ms","94.29ms","95.29ms","96.29ms","97.29ms","98.29ms","99.29ms", },
      new string[] { "0.30ms","1.30ms","2.30ms","3.30ms","4.30ms","5.30ms","6.30ms","7.30ms","8.30ms","9.30ms",    "10.30ms","11.30ms","12.30ms","13.30ms","14.30ms","15.30ms","16.30ms","17.30ms","18.30ms","19.30ms",    "20.30ms","21.30ms","22.30ms","23.30ms","24.30ms","25.30ms","26.30ms","27.30ms","28.30ms","29.30ms",    "30.30ms","31.30ms","32.30ms","33.30ms","34.30ms","35.30ms","36.30ms","37.30ms","38.30ms","39.30ms",    "40.30ms","41.30ms","42.30ms","43.30ms","44.30ms","45.30ms","46.30ms","47.30ms","48.30ms","49.30ms",    "50.30ms","51.30ms","52.30ms","53.30ms","54.30ms","55.30ms","56.30ms","57.30ms","58.30ms","59.30ms",    "60.30ms","61.30ms","62.30ms","63.30ms","64.30ms","65.30ms","66.30ms","67.30ms","68.30ms","69.30ms",    "70.30ms","71.30ms","72.30ms","73.30ms","74.30ms","75.30ms","76.30ms","77.30ms","78.30ms","79.30ms",    "80.30ms","81.30ms","82.30ms","83.30ms","84.30ms","85.30ms","86.30ms","87.30ms","88.30ms","89.30ms",    "90.30ms","91.30ms","92.30ms","93.30ms","94.30ms","95.30ms","96.30ms","97.30ms","98.30ms","99.30ms", },
      new string[] { "0.31ms","1.31ms","2.31ms","3.31ms","4.31ms","5.31ms","6.31ms","7.31ms","8.31ms","9.31ms",    "10.31ms","11.31ms","12.31ms","13.31ms","14.31ms","15.31ms","16.31ms","17.31ms","18.31ms","19.31ms",    "20.31ms","21.31ms","22.31ms","23.31ms","24.31ms","25.31ms","26.31ms","27.31ms","28.31ms","29.31ms",    "30.31ms","31.31ms","32.31ms","33.31ms","34.31ms","35.31ms","36.31ms","37.31ms","38.31ms","39.31ms",    "40.31ms","41.31ms","42.31ms","43.31ms","44.31ms","45.31ms","46.31ms","47.31ms","48.31ms","49.31ms",    "50.31ms","51.31ms","52.31ms","53.31ms","54.31ms","55.31ms","56.31ms","57.31ms","58.31ms","59.31ms",    "60.31ms","61.31ms","62.31ms","63.31ms","64.31ms","65.31ms","66.31ms","67.31ms","68.31ms","69.31ms",    "70.31ms","71.31ms","72.31ms","73.31ms","74.31ms","75.31ms","76.31ms","77.31ms","78.31ms","79.31ms",    "80.31ms","81.31ms","82.31ms","83.31ms","84.31ms","85.31ms","86.31ms","87.31ms","88.31ms","89.31ms",    "90.31ms","91.31ms","92.31ms","93.31ms","94.31ms","95.31ms","96.31ms","97.31ms","98.31ms","99.31ms", },
      new string[] { "0.32ms","1.32ms","2.32ms","3.32ms","4.32ms","5.32ms","6.32ms","7.32ms","8.32ms","9.32ms",    "10.32ms","11.32ms","12.32ms","13.32ms","14.32ms","15.32ms","16.32ms","17.32ms","18.32ms","19.32ms",    "20.32ms","21.32ms","22.32ms","23.32ms","24.32ms","25.32ms","26.32ms","27.32ms","28.32ms","29.32ms",    "30.32ms","31.32ms","32.32ms","33.32ms","34.32ms","35.32ms","36.32ms","37.32ms","38.32ms","39.32ms",    "40.32ms","41.32ms","42.32ms","43.32ms","44.32ms","45.32ms","46.32ms","47.32ms","48.32ms","49.32ms",    "50.32ms","51.32ms","52.32ms","53.32ms","54.32ms","55.32ms","56.32ms","57.32ms","58.32ms","59.32ms",    "60.32ms","61.32ms","62.32ms","63.32ms","64.32ms","65.32ms","66.32ms","67.32ms","68.32ms","69.32ms",    "70.32ms","71.32ms","72.32ms","73.32ms","74.32ms","75.32ms","76.32ms","77.32ms","78.32ms","79.32ms",    "80.32ms","81.32ms","82.32ms","83.32ms","84.32ms","85.32ms","86.32ms","87.32ms","88.32ms","89.32ms",    "90.32ms","91.32ms","92.32ms","93.32ms","94.32ms","95.32ms","96.32ms","97.32ms","98.32ms","99.32ms", },
      new string[] { "0.33ms","1.33ms","2.33ms","3.33ms","4.33ms","5.33ms","6.33ms","7.33ms","8.33ms","9.33ms",    "10.33ms","11.33ms","12.33ms","13.33ms","14.33ms","15.33ms","16.33ms","17.33ms","18.33ms","19.33ms",    "20.33ms","21.33ms","22.33ms","23.33ms","24.33ms","25.33ms","26.33ms","27.33ms","28.33ms","29.33ms",    "30.33ms","31.33ms","32.33ms","33.33ms","34.33ms","35.33ms","36.33ms","37.33ms","38.33ms","39.33ms",    "40.33ms","41.33ms","42.33ms","43.33ms","44.33ms","45.33ms","46.33ms","47.33ms","48.33ms","49.33ms",    "50.33ms","51.33ms","52.33ms","53.33ms","54.33ms","55.33ms","56.33ms","57.33ms","58.33ms","59.33ms",    "60.33ms","61.33ms","62.33ms","63.33ms","64.33ms","65.33ms","66.33ms","67.33ms","68.33ms","69.33ms",    "70.33ms","71.33ms","72.33ms","73.33ms","74.33ms","75.33ms","76.33ms","77.33ms","78.33ms","79.33ms",    "80.33ms","81.33ms","82.33ms","83.33ms","84.33ms","85.33ms","86.33ms","87.33ms","88.33ms","89.33ms",    "90.33ms","91.33ms","92.33ms","93.33ms","94.33ms","95.33ms","96.33ms","97.33ms","98.33ms","99.33ms", },
      new string[] { "0.34ms","1.34ms","2.34ms","3.34ms","4.34ms","5.34ms","6.34ms","7.34ms","8.34ms","9.34ms",    "10.34ms","11.34ms","12.34ms","13.34ms","14.34ms","15.34ms","16.34ms","17.34ms","18.34ms","19.34ms",    "20.34ms","21.34ms","22.34ms","23.34ms","24.34ms","25.34ms","26.34ms","27.34ms","28.34ms","29.34ms",    "30.34ms","31.34ms","32.34ms","33.34ms","34.34ms","35.34ms","36.34ms","37.34ms","38.34ms","39.34ms",    "40.34ms","41.34ms","42.34ms","43.34ms","44.34ms","45.34ms","46.34ms","47.34ms","48.34ms","49.34ms",    "50.34ms","51.34ms","52.34ms","53.34ms","54.34ms","55.34ms","56.34ms","57.34ms","58.34ms","59.34ms",    "60.34ms","61.34ms","62.34ms","63.34ms","64.34ms","65.34ms","66.34ms","67.34ms","68.34ms","69.34ms",    "70.34ms","71.34ms","72.34ms","73.34ms","74.34ms","75.34ms","76.34ms","77.34ms","78.34ms","79.34ms",    "80.34ms","81.34ms","82.34ms","83.34ms","84.34ms","85.34ms","86.34ms","87.34ms","88.34ms","89.34ms",    "90.34ms","91.34ms","92.34ms","93.34ms","94.34ms","95.34ms","96.34ms","97.34ms","98.34ms","99.34ms", },
      new string[] { "0.35ms","1.35ms","2.35ms","3.35ms","4.35ms","5.35ms","6.35ms","7.35ms","8.35ms","9.35ms",    "10.35ms","11.35ms","12.35ms","13.35ms","14.35ms","15.35ms","16.35ms","17.35ms","18.35ms","19.35ms",    "20.35ms","21.35ms","22.35ms","23.35ms","24.35ms","25.35ms","26.35ms","27.35ms","28.35ms","29.35ms",    "30.35ms","31.35ms","32.35ms","33.35ms","34.35ms","35.35ms","36.35ms","37.35ms","38.35ms","39.35ms",    "40.35ms","41.35ms","42.35ms","43.35ms","44.35ms","45.35ms","46.35ms","47.35ms","48.35ms","49.35ms",    "50.35ms","51.35ms","52.35ms","53.35ms","54.35ms","55.35ms","56.35ms","57.35ms","58.35ms","59.35ms",    "60.35ms","61.35ms","62.35ms","63.35ms","64.35ms","65.35ms","66.35ms","67.35ms","68.35ms","69.35ms",    "70.35ms","71.35ms","72.35ms","73.35ms","74.35ms","75.35ms","76.35ms","77.35ms","78.35ms","79.35ms",    "80.35ms","81.35ms","82.35ms","83.35ms","84.35ms","85.35ms","86.35ms","87.35ms","88.35ms","89.35ms",    "90.35ms","91.35ms","92.35ms","93.35ms","94.35ms","95.35ms","96.35ms","97.35ms","98.35ms","99.35ms", },
      new string[] { "0.36ms","1.36ms","2.36ms","3.36ms","4.36ms","5.36ms","6.36ms","7.36ms","8.36ms","9.36ms",    "10.36ms","11.36ms","12.36ms","13.36ms","14.36ms","15.36ms","16.36ms","17.36ms","18.36ms","19.36ms",    "20.36ms","21.36ms","22.36ms","23.36ms","24.36ms","25.36ms","26.36ms","27.36ms","28.36ms","29.36ms",    "30.36ms","31.36ms","32.36ms","33.36ms","34.36ms","35.36ms","36.36ms","37.36ms","38.36ms","39.36ms",    "40.36ms","41.36ms","42.36ms","43.36ms","44.36ms","45.36ms","46.36ms","47.36ms","48.36ms","49.36ms",    "50.36ms","51.36ms","52.36ms","53.36ms","54.36ms","55.36ms","56.36ms","57.36ms","58.36ms","59.36ms",    "60.36ms","61.36ms","62.36ms","63.36ms","64.36ms","65.36ms","66.36ms","67.36ms","68.36ms","69.36ms",    "70.36ms","71.36ms","72.36ms","73.36ms","74.36ms","75.36ms","76.36ms","77.36ms","78.36ms","79.36ms",    "80.36ms","81.36ms","82.36ms","83.36ms","84.36ms","85.36ms","86.36ms","87.36ms","88.36ms","89.36ms",    "90.36ms","91.36ms","92.36ms","93.36ms","94.36ms","95.36ms","96.36ms","97.36ms","98.36ms","99.36ms", },
      new string[] { "0.37ms","1.37ms","2.37ms","3.37ms","4.37ms","5.37ms","6.37ms","7.37ms","8.37ms","9.37ms",    "10.37ms","11.37ms","12.37ms","13.37ms","14.37ms","15.37ms","16.37ms","17.37ms","18.37ms","19.37ms",    "20.37ms","21.37ms","22.37ms","23.37ms","24.37ms","25.37ms","26.37ms","27.37ms","28.37ms","29.37ms",    "30.37ms","31.37ms","32.37ms","33.37ms","34.37ms","35.37ms","36.37ms","37.37ms","38.37ms","39.37ms",    "40.37ms","41.37ms","42.37ms","43.37ms","44.37ms","45.37ms","46.37ms","47.37ms","48.37ms","49.37ms",    "50.37ms","51.37ms","52.37ms","53.37ms","54.37ms","55.37ms","56.37ms","57.37ms","58.37ms","59.37ms",    "60.37ms","61.37ms","62.37ms","63.37ms","64.37ms","65.37ms","66.37ms","67.37ms","68.37ms","69.37ms",    "70.37ms","71.37ms","72.37ms","73.37ms","74.37ms","75.37ms","76.37ms","77.37ms","78.37ms","79.37ms",    "80.37ms","81.37ms","82.37ms","83.37ms","84.37ms","85.37ms","86.37ms","87.37ms","88.37ms","89.37ms",    "90.37ms","91.37ms","92.37ms","93.37ms","94.37ms","95.37ms","96.37ms","97.37ms","98.37ms","99.37ms", },
      new string[] { "0.38ms","1.38ms","2.38ms","3.38ms","4.38ms","5.38ms","6.38ms","7.38ms","8.38ms","9.38ms",    "10.38ms","11.38ms","12.38ms","13.38ms","14.38ms","15.38ms","16.38ms","17.38ms","18.38ms","19.38ms",    "20.38ms","21.38ms","22.38ms","23.38ms","24.38ms","25.38ms","26.38ms","27.38ms","28.38ms","29.38ms",    "30.38ms","31.38ms","32.38ms","33.38ms","34.38ms","35.38ms","36.38ms","37.38ms","38.38ms","39.38ms",    "40.38ms","41.38ms","42.38ms","43.38ms","44.38ms","45.38ms","46.38ms","47.38ms","48.38ms","49.38ms",    "50.38ms","51.38ms","52.38ms","53.38ms","54.38ms","55.38ms","56.38ms","57.38ms","58.38ms","59.38ms",    "60.38ms","61.38ms","62.38ms","63.38ms","64.38ms","65.38ms","66.38ms","67.38ms","68.38ms","69.38ms",    "70.38ms","71.38ms","72.38ms","73.38ms","74.38ms","75.38ms","76.38ms","77.38ms","78.38ms","79.38ms",    "80.38ms","81.38ms","82.38ms","83.38ms","84.38ms","85.38ms","86.38ms","87.38ms","88.38ms","89.38ms",    "90.38ms","91.38ms","92.38ms","93.38ms","94.38ms","95.38ms","96.38ms","97.38ms","98.38ms","99.38ms", },
      new string[] { "0.39ms","1.39ms","2.39ms","3.39ms","4.39ms","5.39ms","6.39ms","7.39ms","8.39ms","9.39ms",    "10.39ms","11.39ms","12.39ms","13.39ms","14.39ms","15.39ms","16.39ms","17.39ms","18.39ms","19.39ms",    "20.39ms","21.39ms","22.39ms","23.39ms","24.39ms","25.39ms","26.39ms","27.39ms","28.39ms","29.39ms",    "30.39ms","31.39ms","32.39ms","33.39ms","34.39ms","35.39ms","36.39ms","37.39ms","38.39ms","39.39ms",    "40.39ms","41.39ms","42.39ms","43.39ms","44.39ms","45.39ms","46.39ms","47.39ms","48.39ms","49.39ms",    "50.39ms","51.39ms","52.39ms","53.39ms","54.39ms","55.39ms","56.39ms","57.39ms","58.39ms","59.39ms",    "60.39ms","61.39ms","62.39ms","63.39ms","64.39ms","65.39ms","66.39ms","67.39ms","68.39ms","69.39ms",    "70.39ms","71.39ms","72.39ms","73.39ms","74.39ms","75.39ms","76.39ms","77.39ms","78.39ms","79.39ms",    "80.39ms","81.39ms","82.39ms","83.39ms","84.39ms","85.39ms","86.39ms","87.39ms","88.39ms","89.39ms",    "90.39ms","91.39ms","92.39ms","93.39ms","94.39ms","95.39ms","96.39ms","97.39ms","98.39ms","99.39ms", },
      new string[] { "0.40ms","1.40ms","2.40ms","3.40ms","4.40ms","5.40ms","6.40ms","7.40ms","8.40ms","9.40ms",    "10.40ms","11.40ms","12.40ms","13.40ms","14.40ms","15.40ms","16.40ms","17.40ms","18.40ms","19.40ms",    "20.40ms","21.40ms","22.40ms","23.40ms","24.40ms","25.40ms","26.40ms","27.40ms","28.40ms","29.40ms",    "30.40ms","31.40ms","32.40ms","33.40ms","34.40ms","35.40ms","36.40ms","37.40ms","38.40ms","39.40ms",    "40.40ms","41.40ms","42.40ms","43.40ms","44.40ms","45.40ms","46.40ms","47.40ms","48.40ms","49.40ms",    "50.40ms","51.40ms","52.40ms","53.40ms","54.40ms","55.40ms","56.40ms","57.40ms","58.40ms","59.40ms",    "60.40ms","61.40ms","62.40ms","63.40ms","64.40ms","65.40ms","66.40ms","67.40ms","68.40ms","69.40ms",    "70.40ms","71.40ms","72.40ms","73.40ms","74.40ms","75.40ms","76.40ms","77.40ms","78.40ms","79.40ms",    "80.40ms","81.40ms","82.40ms","83.40ms","84.40ms","85.40ms","86.40ms","87.40ms","88.40ms","89.40ms",    "90.40ms","91.40ms","92.40ms","93.40ms","94.40ms","95.40ms","96.40ms","97.40ms","98.40ms","99.40ms", },
      new string[] { "0.41ms","1.41ms","2.41ms","3.41ms","4.41ms","5.41ms","6.41ms","7.41ms","8.41ms","9.41ms",    "10.41ms","11.41ms","12.41ms","13.41ms","14.41ms","15.41ms","16.41ms","17.41ms","18.41ms","19.41ms",    "20.41ms","21.41ms","22.41ms","23.41ms","24.41ms","25.41ms","26.41ms","27.41ms","28.41ms","29.41ms",    "30.41ms","31.41ms","32.41ms","33.41ms","34.41ms","35.41ms","36.41ms","37.41ms","38.41ms","39.41ms",    "40.41ms","41.41ms","42.41ms","43.41ms","44.41ms","45.41ms","46.41ms","47.41ms","48.41ms","49.41ms",    "50.41ms","51.41ms","52.41ms","53.41ms","54.41ms","55.41ms","56.41ms","57.41ms","58.41ms","59.41ms",    "60.41ms","61.41ms","62.41ms","63.41ms","64.41ms","65.41ms","66.41ms","67.41ms","68.41ms","69.41ms",    "70.41ms","71.41ms","72.41ms","73.41ms","74.41ms","75.41ms","76.41ms","77.41ms","78.41ms","79.41ms",    "80.41ms","81.41ms","82.41ms","83.41ms","84.41ms","85.41ms","86.41ms","87.41ms","88.41ms","89.41ms",    "90.41ms","91.41ms","92.41ms","93.41ms","94.41ms","95.41ms","96.41ms","97.41ms","98.41ms","99.41ms", },
      new string[] { "0.42ms","1.42ms","2.42ms","3.42ms","4.42ms","5.42ms","6.42ms","7.42ms","8.42ms","9.42ms",    "10.42ms","11.42ms","12.42ms","13.42ms","14.42ms","15.42ms","16.42ms","17.42ms","18.42ms","19.42ms",    "20.42ms","21.42ms","22.42ms","23.42ms","24.42ms","25.42ms","26.42ms","27.42ms","28.42ms","29.42ms",    "30.42ms","31.42ms","32.42ms","33.42ms","34.42ms","35.42ms","36.42ms","37.42ms","38.42ms","39.42ms",    "40.42ms","41.42ms","42.42ms","43.42ms","44.42ms","45.42ms","46.42ms","47.42ms","48.42ms","49.42ms",    "50.42ms","51.42ms","52.42ms","53.42ms","54.42ms","55.42ms","56.42ms","57.42ms","58.42ms","59.42ms",    "60.42ms","61.42ms","62.42ms","63.42ms","64.42ms","65.42ms","66.42ms","67.42ms","68.42ms","69.42ms",    "70.42ms","71.42ms","72.42ms","73.42ms","74.42ms","75.42ms","76.42ms","77.42ms","78.42ms","79.42ms",    "80.42ms","81.42ms","82.42ms","83.42ms","84.42ms","85.42ms","86.42ms","87.42ms","88.42ms","89.42ms",    "90.42ms","91.42ms","92.42ms","93.42ms","94.42ms","95.42ms","96.42ms","97.42ms","98.42ms","99.42ms", },
      new string[] { "0.43ms","1.43ms","2.43ms","3.43ms","4.43ms","5.43ms","6.43ms","7.43ms","8.43ms","9.43ms",    "10.43ms","11.43ms","12.43ms","13.43ms","14.43ms","15.43ms","16.43ms","17.43ms","18.43ms","19.43ms",    "20.43ms","21.43ms","22.43ms","23.43ms","24.43ms","25.43ms","26.43ms","27.43ms","28.43ms","29.43ms",    "30.43ms","31.43ms","32.43ms","33.43ms","34.43ms","35.43ms","36.43ms","37.43ms","38.43ms","39.43ms",    "40.43ms","41.43ms","42.43ms","43.43ms","44.43ms","45.43ms","46.43ms","47.43ms","48.43ms","49.43ms",    "50.43ms","51.43ms","52.43ms","53.43ms","54.43ms","55.43ms","56.43ms","57.43ms","58.43ms","59.43ms",    "60.43ms","61.43ms","62.43ms","63.43ms","64.43ms","65.43ms","66.43ms","67.43ms","68.43ms","69.43ms",    "70.43ms","71.43ms","72.43ms","73.43ms","74.43ms","75.43ms","76.43ms","77.43ms","78.43ms","79.43ms",    "80.43ms","81.43ms","82.43ms","83.43ms","84.43ms","85.43ms","86.43ms","87.43ms","88.43ms","89.43ms",    "90.43ms","91.43ms","92.43ms","93.43ms","94.43ms","95.43ms","96.43ms","97.43ms","98.43ms","99.43ms", },
      new string[] { "0.44ms","1.44ms","2.44ms","3.44ms","4.44ms","5.44ms","6.44ms","7.44ms","8.44ms","9.44ms",    "10.44ms","11.44ms","12.44ms","13.44ms","14.44ms","15.44ms","16.44ms","17.44ms","18.44ms","19.44ms",    "20.44ms","21.44ms","22.44ms","23.44ms","24.44ms","25.44ms","26.44ms","27.44ms","28.44ms","29.44ms",    "30.44ms","31.44ms","32.44ms","33.44ms","34.44ms","35.44ms","36.44ms","37.44ms","38.44ms","39.44ms",    "40.44ms","41.44ms","42.44ms","43.44ms","44.44ms","45.44ms","46.44ms","47.44ms","48.44ms","49.44ms",    "50.44ms","51.44ms","52.44ms","53.44ms","54.44ms","55.44ms","56.44ms","57.44ms","58.44ms","59.44ms",    "60.44ms","61.44ms","62.44ms","63.44ms","64.44ms","65.44ms","66.44ms","67.44ms","68.44ms","69.44ms",    "70.44ms","71.44ms","72.44ms","73.44ms","74.44ms","75.44ms","76.44ms","77.44ms","78.44ms","79.44ms",    "80.44ms","81.44ms","82.44ms","83.44ms","84.44ms","85.44ms","86.44ms","87.44ms","88.44ms","89.44ms",    "90.44ms","91.44ms","92.44ms","93.44ms","94.44ms","95.44ms","96.44ms","97.44ms","98.44ms","99.44ms", },
      new string[] { "0.45ms","1.45ms","2.45ms","3.45ms","4.45ms","5.45ms","6.45ms","7.45ms","8.45ms","9.45ms",    "10.45ms","11.45ms","12.45ms","13.45ms","14.45ms","15.45ms","16.45ms","17.45ms","18.45ms","19.45ms",    "20.45ms","21.45ms","22.45ms","23.45ms","24.45ms","25.45ms","26.45ms","27.45ms","28.45ms","29.45ms",    "30.45ms","31.45ms","32.45ms","33.45ms","34.45ms","35.45ms","36.45ms","37.45ms","38.45ms","39.45ms",    "40.45ms","41.45ms","42.45ms","43.45ms","44.45ms","45.45ms","46.45ms","47.45ms","48.45ms","49.45ms",    "50.45ms","51.45ms","52.45ms","53.45ms","54.45ms","55.45ms","56.45ms","57.45ms","58.45ms","59.45ms",    "60.45ms","61.45ms","62.45ms","63.45ms","64.45ms","65.45ms","66.45ms","67.45ms","68.45ms","69.45ms",    "70.45ms","71.45ms","72.45ms","73.45ms","74.45ms","75.45ms","76.45ms","77.45ms","78.45ms","79.45ms",    "80.45ms","81.45ms","82.45ms","83.45ms","84.45ms","85.45ms","86.45ms","87.45ms","88.45ms","89.45ms",    "90.45ms","91.45ms","92.45ms","93.45ms","94.45ms","95.45ms","96.45ms","97.45ms","98.45ms","99.45ms", },
      new string[] { "0.46ms","1.46ms","2.46ms","3.46ms","4.46ms","5.46ms","6.46ms","7.46ms","8.46ms","9.46ms",    "10.46ms","11.46ms","12.46ms","13.46ms","14.46ms","15.46ms","16.46ms","17.46ms","18.46ms","19.46ms",    "20.46ms","21.46ms","22.46ms","23.46ms","24.46ms","25.46ms","26.46ms","27.46ms","28.46ms","29.46ms",    "30.46ms","31.46ms","32.46ms","33.46ms","34.46ms","35.46ms","36.46ms","37.46ms","38.46ms","39.46ms",    "40.46ms","41.46ms","42.46ms","43.46ms","44.46ms","45.46ms","46.46ms","47.46ms","48.46ms","49.46ms",    "50.46ms","51.46ms","52.46ms","53.46ms","54.46ms","55.46ms","56.46ms","57.46ms","58.46ms","59.46ms",    "60.46ms","61.46ms","62.46ms","63.46ms","64.46ms","65.46ms","66.46ms","67.46ms","68.46ms","69.46ms",    "70.46ms","71.46ms","72.46ms","73.46ms","74.46ms","75.46ms","76.46ms","77.46ms","78.46ms","79.46ms",    "80.46ms","81.46ms","82.46ms","83.46ms","84.46ms","85.46ms","86.46ms","87.46ms","88.46ms","89.46ms",    "90.46ms","91.46ms","92.46ms","93.46ms","94.46ms","95.46ms","96.46ms","97.46ms","98.46ms","99.46ms", },
      new string[] { "0.47ms","1.47ms","2.47ms","3.47ms","4.47ms","5.47ms","6.47ms","7.47ms","8.47ms","9.47ms",    "10.47ms","11.47ms","12.47ms","13.47ms","14.47ms","15.47ms","16.47ms","17.47ms","18.47ms","19.47ms",    "20.47ms","21.47ms","22.47ms","23.47ms","24.47ms","25.47ms","26.47ms","27.47ms","28.47ms","29.47ms",    "30.47ms","31.47ms","32.47ms","33.47ms","34.47ms","35.47ms","36.47ms","37.47ms","38.47ms","39.47ms",    "40.47ms","41.47ms","42.47ms","43.47ms","44.47ms","45.47ms","46.47ms","47.47ms","48.47ms","49.47ms",    "50.47ms","51.47ms","52.47ms","53.47ms","54.47ms","55.47ms","56.47ms","57.47ms","58.47ms","59.47ms",    "60.47ms","61.47ms","62.47ms","63.47ms","64.47ms","65.47ms","66.47ms","67.47ms","68.47ms","69.47ms",    "70.47ms","71.47ms","72.47ms","73.47ms","74.47ms","75.47ms","76.47ms","77.47ms","78.47ms","79.47ms",    "80.47ms","81.47ms","82.47ms","83.47ms","84.47ms","85.47ms","86.47ms","87.47ms","88.47ms","89.47ms",    "90.47ms","91.47ms","92.47ms","93.47ms","94.47ms","95.47ms","96.47ms","97.47ms","98.47ms","99.47ms", },
      new string[] { "0.48ms","1.48ms","2.48ms","3.48ms","4.48ms","5.48ms","6.48ms","7.48ms","8.48ms","9.48ms",    "10.48ms","11.48ms","12.48ms","13.48ms","14.48ms","15.48ms","16.48ms","17.48ms","18.48ms","19.48ms",    "20.48ms","21.48ms","22.48ms","23.48ms","24.48ms","25.48ms","26.48ms","27.48ms","28.48ms","29.48ms",    "30.48ms","31.48ms","32.48ms","33.48ms","34.48ms","35.48ms","36.48ms","37.48ms","38.48ms","39.48ms",    "40.48ms","41.48ms","42.48ms","43.48ms","44.48ms","45.48ms","46.48ms","47.48ms","48.48ms","49.48ms",    "50.48ms","51.48ms","52.48ms","53.48ms","54.48ms","55.48ms","56.48ms","57.48ms","58.48ms","59.48ms",    "60.48ms","61.48ms","62.48ms","63.48ms","64.48ms","65.48ms","66.48ms","67.48ms","68.48ms","69.48ms",    "70.48ms","71.48ms","72.48ms","73.48ms","74.48ms","75.48ms","76.48ms","77.48ms","78.48ms","79.48ms",    "80.48ms","81.48ms","82.48ms","83.48ms","84.48ms","85.48ms","86.48ms","87.48ms","88.48ms","89.48ms",    "90.48ms","91.48ms","92.48ms","93.48ms","94.48ms","95.48ms","96.48ms","97.48ms","98.48ms","99.48ms", },
      new string[] { "0.49ms","1.49ms","2.49ms","3.49ms","4.49ms","5.49ms","6.49ms","7.49ms","8.49ms","9.49ms",    "10.49ms","11.49ms","12.49ms","13.49ms","14.49ms","15.49ms","16.49ms","17.49ms","18.49ms","19.49ms",    "20.49ms","21.49ms","22.49ms","23.49ms","24.49ms","25.49ms","26.49ms","27.49ms","28.49ms","29.49ms",    "30.49ms","31.49ms","32.49ms","33.49ms","34.49ms","35.49ms","36.49ms","37.49ms","38.49ms","39.49ms",    "40.49ms","41.49ms","42.49ms","43.49ms","44.49ms","45.49ms","46.49ms","47.49ms","48.49ms","49.49ms",    "50.49ms","51.49ms","52.49ms","53.49ms","54.49ms","55.49ms","56.49ms","57.49ms","58.49ms","59.49ms",    "60.49ms","61.49ms","62.49ms","63.49ms","64.49ms","65.49ms","66.49ms","67.49ms","68.49ms","69.49ms",    "70.49ms","71.49ms","72.49ms","73.49ms","74.49ms","75.49ms","76.49ms","77.49ms","78.49ms","79.49ms",    "80.49ms","81.49ms","82.49ms","83.49ms","84.49ms","85.49ms","86.49ms","87.49ms","88.49ms","89.49ms",    "90.49ms","91.49ms","92.49ms","93.49ms","94.49ms","95.49ms","96.49ms","97.49ms","98.49ms","99.49ms", },
      new string[] { "0.50ms","1.50ms","2.50ms","3.50ms","4.50ms","5.50ms","6.50ms","7.50ms","8.50ms","9.50ms",    "10.50ms","11.50ms","12.50ms","13.50ms","14.50ms","15.50ms","16.50ms","17.50ms","18.50ms","19.50ms",    "20.50ms","21.50ms","22.50ms","23.50ms","24.50ms","25.50ms","26.50ms","27.50ms","28.50ms","29.50ms",    "30.50ms","31.50ms","32.50ms","33.50ms","34.50ms","35.50ms","36.50ms","37.50ms","38.50ms","39.50ms",    "40.50ms","41.50ms","42.50ms","43.50ms","44.50ms","45.50ms","46.50ms","47.50ms","48.50ms","49.50ms",    "50.50ms","51.50ms","52.50ms","53.50ms","54.50ms","55.50ms","56.50ms","57.50ms","58.50ms","59.50ms",    "60.50ms","61.50ms","62.50ms","63.50ms","64.50ms","65.50ms","66.50ms","67.50ms","68.50ms","69.50ms",    "70.50ms","71.50ms","72.50ms","73.50ms","74.50ms","75.50ms","76.50ms","77.50ms","78.50ms","79.50ms",    "80.50ms","81.50ms","82.50ms","83.50ms","84.50ms","85.50ms","86.50ms","87.50ms","88.50ms","89.50ms",    "90.50ms","91.50ms","92.50ms","93.50ms","94.50ms","95.50ms","96.50ms","97.50ms","98.50ms","99.50ms", },
      new string[] { "0.51ms","1.51ms","2.51ms","3.51ms","4.51ms","5.51ms","6.51ms","7.51ms","8.51ms","9.51ms",    "10.51ms","11.51ms","12.51ms","13.51ms","14.51ms","15.51ms","16.51ms","17.51ms","18.51ms","19.51ms",    "20.51ms","21.51ms","22.51ms","23.51ms","24.51ms","25.51ms","26.51ms","27.51ms","28.51ms","29.51ms",    "30.51ms","31.51ms","32.51ms","33.51ms","34.51ms","35.51ms","36.51ms","37.51ms","38.51ms","39.51ms",    "40.51ms","41.51ms","42.51ms","43.51ms","44.51ms","45.51ms","46.51ms","47.51ms","48.51ms","49.51ms",    "50.51ms","51.51ms","52.51ms","53.51ms","54.51ms","55.51ms","56.51ms","57.51ms","58.51ms","59.51ms",    "60.51ms","61.51ms","62.51ms","63.51ms","64.51ms","65.51ms","66.51ms","67.51ms","68.51ms","69.51ms",    "70.51ms","71.51ms","72.51ms","73.51ms","74.51ms","75.51ms","76.51ms","77.51ms","78.51ms","79.51ms",    "80.51ms","81.51ms","82.51ms","83.51ms","84.51ms","85.51ms","86.51ms","87.51ms","88.51ms","89.51ms",    "90.51ms","91.51ms","92.51ms","93.51ms","94.51ms","95.51ms","96.51ms","97.51ms","98.51ms","99.51ms", },
      new string[] { "0.52ms","1.52ms","2.52ms","3.52ms","4.52ms","5.52ms","6.52ms","7.52ms","8.52ms","9.52ms",    "10.52ms","11.52ms","12.52ms","13.52ms","14.52ms","15.52ms","16.52ms","17.52ms","18.52ms","19.52ms",    "20.52ms","21.52ms","22.52ms","23.52ms","24.52ms","25.52ms","26.52ms","27.52ms","28.52ms","29.52ms",    "30.52ms","31.52ms","32.52ms","33.52ms","34.52ms","35.52ms","36.52ms","37.52ms","38.52ms","39.52ms",    "40.52ms","41.52ms","42.52ms","43.52ms","44.52ms","45.52ms","46.52ms","47.52ms","48.52ms","49.52ms",    "50.52ms","51.52ms","52.52ms","53.52ms","54.52ms","55.52ms","56.52ms","57.52ms","58.52ms","59.52ms",    "60.52ms","61.52ms","62.52ms","63.52ms","64.52ms","65.52ms","66.52ms","67.52ms","68.52ms","69.52ms",    "70.52ms","71.52ms","72.52ms","73.52ms","74.52ms","75.52ms","76.52ms","77.52ms","78.52ms","79.52ms",    "80.52ms","81.52ms","82.52ms","83.52ms","84.52ms","85.52ms","86.52ms","87.52ms","88.52ms","89.52ms",    "90.52ms","91.52ms","92.52ms","93.52ms","94.52ms","95.52ms","96.52ms","97.52ms","98.52ms","99.52ms", },
      new string[] { "0.53ms","1.53ms","2.53ms","3.53ms","4.53ms","5.53ms","6.53ms","7.53ms","8.53ms","9.53ms",    "10.53ms","11.53ms","12.53ms","13.53ms","14.53ms","15.53ms","16.53ms","17.53ms","18.53ms","19.53ms",    "20.53ms","21.53ms","22.53ms","23.53ms","24.53ms","25.53ms","26.53ms","27.53ms","28.53ms","29.53ms",    "30.53ms","31.53ms","32.53ms","33.53ms","34.53ms","35.53ms","36.53ms","37.53ms","38.53ms","39.53ms",    "40.53ms","41.53ms","42.53ms","43.53ms","44.53ms","45.53ms","46.53ms","47.53ms","48.53ms","49.53ms",    "50.53ms","51.53ms","52.53ms","53.53ms","54.53ms","55.53ms","56.53ms","57.53ms","58.53ms","59.53ms",    "60.53ms","61.53ms","62.53ms","63.53ms","64.53ms","65.53ms","66.53ms","67.53ms","68.53ms","69.53ms",    "70.53ms","71.53ms","72.53ms","73.53ms","74.53ms","75.53ms","76.53ms","77.53ms","78.53ms","79.53ms",    "80.53ms","81.53ms","82.53ms","83.53ms","84.53ms","85.53ms","86.53ms","87.53ms","88.53ms","89.53ms",    "90.53ms","91.53ms","92.53ms","93.53ms","94.53ms","95.53ms","96.53ms","97.53ms","98.53ms","99.53ms", },
      new string[] { "0.54ms","1.54ms","2.54ms","3.54ms","4.54ms","5.54ms","6.54ms","7.54ms","8.54ms","9.54ms",    "10.54ms","11.54ms","12.54ms","13.54ms","14.54ms","15.54ms","16.54ms","17.54ms","18.54ms","19.54ms",    "20.54ms","21.54ms","22.54ms","23.54ms","24.54ms","25.54ms","26.54ms","27.54ms","28.54ms","29.54ms",    "30.54ms","31.54ms","32.54ms","33.54ms","34.54ms","35.54ms","36.54ms","37.54ms","38.54ms","39.54ms",    "40.54ms","41.54ms","42.54ms","43.54ms","44.54ms","45.54ms","46.54ms","47.54ms","48.54ms","49.54ms",    "50.54ms","51.54ms","52.54ms","53.54ms","54.54ms","55.54ms","56.54ms","57.54ms","58.54ms","59.54ms",    "60.54ms","61.54ms","62.54ms","63.54ms","64.54ms","65.54ms","66.54ms","67.54ms","68.54ms","69.54ms",    "70.54ms","71.54ms","72.54ms","73.54ms","74.54ms","75.54ms","76.54ms","77.54ms","78.54ms","79.54ms",    "80.54ms","81.54ms","82.54ms","83.54ms","84.54ms","85.54ms","86.54ms","87.54ms","88.54ms","89.54ms",    "90.54ms","91.54ms","92.54ms","93.54ms","94.54ms","95.54ms","96.54ms","97.54ms","98.54ms","99.54ms", },
      new string[] { "0.55ms","1.55ms","2.55ms","3.55ms","4.55ms","5.55ms","6.55ms","7.55ms","8.55ms","9.55ms",    "10.55ms","11.55ms","12.55ms","13.55ms","14.55ms","15.55ms","16.55ms","17.55ms","18.55ms","19.55ms",    "20.55ms","21.55ms","22.55ms","23.55ms","24.55ms","25.55ms","26.55ms","27.55ms","28.55ms","29.55ms",    "30.55ms","31.55ms","32.55ms","33.55ms","34.55ms","35.55ms","36.55ms","37.55ms","38.55ms","39.55ms",    "40.55ms","41.55ms","42.55ms","43.55ms","44.55ms","45.55ms","46.55ms","47.55ms","48.55ms","49.55ms",    "50.55ms","51.55ms","52.55ms","53.55ms","54.55ms","55.55ms","56.55ms","57.55ms","58.55ms","59.55ms",    "60.55ms","61.55ms","62.55ms","63.55ms","64.55ms","65.55ms","66.55ms","67.55ms","68.55ms","69.55ms",    "70.55ms","71.55ms","72.55ms","73.55ms","74.55ms","75.55ms","76.55ms","77.55ms","78.55ms","79.55ms",    "80.55ms","81.55ms","82.55ms","83.55ms","84.55ms","85.55ms","86.55ms","87.55ms","88.55ms","89.55ms",    "90.55ms","91.55ms","92.55ms","93.55ms","94.55ms","95.55ms","96.55ms","97.55ms","98.55ms","99.55ms", },
      new string[] { "0.56ms","1.56ms","2.56ms","3.56ms","4.56ms","5.56ms","6.56ms","7.56ms","8.56ms","9.56ms",    "10.56ms","11.56ms","12.56ms","13.56ms","14.56ms","15.56ms","16.56ms","17.56ms","18.56ms","19.56ms",    "20.56ms","21.56ms","22.56ms","23.56ms","24.56ms","25.56ms","26.56ms","27.56ms","28.56ms","29.56ms",    "30.56ms","31.56ms","32.56ms","33.56ms","34.56ms","35.56ms","36.56ms","37.56ms","38.56ms","39.56ms",    "40.56ms","41.56ms","42.56ms","43.56ms","44.56ms","45.56ms","46.56ms","47.56ms","48.56ms","49.56ms",    "50.56ms","51.56ms","52.56ms","53.56ms","54.56ms","55.56ms","56.56ms","57.56ms","58.56ms","59.56ms",    "60.56ms","61.56ms","62.56ms","63.56ms","64.56ms","65.56ms","66.56ms","67.56ms","68.56ms","69.56ms",    "70.56ms","71.56ms","72.56ms","73.56ms","74.56ms","75.56ms","76.56ms","77.56ms","78.56ms","79.56ms",    "80.56ms","81.56ms","82.56ms","83.56ms","84.56ms","85.56ms","86.56ms","87.56ms","88.56ms","89.56ms",    "90.56ms","91.56ms","92.56ms","93.56ms","94.56ms","95.56ms","96.56ms","97.56ms","98.56ms","99.56ms", },
      new string[] { "0.57ms","1.57ms","2.57ms","3.57ms","4.57ms","5.57ms","6.57ms","7.57ms","8.57ms","9.57ms",    "10.57ms","11.57ms","12.57ms","13.57ms","14.57ms","15.57ms","16.57ms","17.57ms","18.57ms","19.57ms",    "20.57ms","21.57ms","22.57ms","23.57ms","24.57ms","25.57ms","26.57ms","27.57ms","28.57ms","29.57ms",    "30.57ms","31.57ms","32.57ms","33.57ms","34.57ms","35.57ms","36.57ms","37.57ms","38.57ms","39.57ms",    "40.57ms","41.57ms","42.57ms","43.57ms","44.57ms","45.57ms","46.57ms","47.57ms","48.57ms","49.57ms",    "50.57ms","51.57ms","52.57ms","53.57ms","54.57ms","55.57ms","56.57ms","57.57ms","58.57ms","59.57ms",    "60.57ms","61.57ms","62.57ms","63.57ms","64.57ms","65.57ms","66.57ms","67.57ms","68.57ms","69.57ms",    "70.57ms","71.57ms","72.57ms","73.57ms","74.57ms","75.57ms","76.57ms","77.57ms","78.57ms","79.57ms",    "80.57ms","81.57ms","82.57ms","83.57ms","84.57ms","85.57ms","86.57ms","87.57ms","88.57ms","89.57ms",    "90.57ms","91.57ms","92.57ms","93.57ms","94.57ms","95.57ms","96.57ms","97.57ms","98.57ms","99.57ms", },
      new string[] { "0.58ms","1.58ms","2.58ms","3.58ms","4.58ms","5.58ms","6.58ms","7.58ms","8.58ms","9.58ms",    "10.58ms","11.58ms","12.58ms","13.58ms","14.58ms","15.58ms","16.58ms","17.58ms","18.58ms","19.58ms",    "20.58ms","21.58ms","22.58ms","23.58ms","24.58ms","25.58ms","26.58ms","27.58ms","28.58ms","29.58ms",    "30.58ms","31.58ms","32.58ms","33.58ms","34.58ms","35.58ms","36.58ms","37.58ms","38.58ms","39.58ms",    "40.58ms","41.58ms","42.58ms","43.58ms","44.58ms","45.58ms","46.58ms","47.58ms","48.58ms","49.58ms",    "50.58ms","51.58ms","52.58ms","53.58ms","54.58ms","55.58ms","56.58ms","57.58ms","58.58ms","59.58ms",    "60.58ms","61.58ms","62.58ms","63.58ms","64.58ms","65.58ms","66.58ms","67.58ms","68.58ms","69.58ms",    "70.58ms","71.58ms","72.58ms","73.58ms","74.58ms","75.58ms","76.58ms","77.58ms","78.58ms","79.58ms",    "80.58ms","81.58ms","82.58ms","83.58ms","84.58ms","85.58ms","86.58ms","87.58ms","88.58ms","89.58ms",    "90.58ms","91.58ms","92.58ms","93.58ms","94.58ms","95.58ms","96.58ms","97.58ms","98.58ms","99.58ms", },
      new string[] { "0.59ms","1.59ms","2.59ms","3.59ms","4.59ms","5.59ms","6.59ms","7.59ms","8.59ms","9.59ms",    "10.59ms","11.59ms","12.59ms","13.59ms","14.59ms","15.59ms","16.59ms","17.59ms","18.59ms","19.59ms",    "20.59ms","21.59ms","22.59ms","23.59ms","24.59ms","25.59ms","26.59ms","27.59ms","28.59ms","29.59ms",    "30.59ms","31.59ms","32.59ms","33.59ms","34.59ms","35.59ms","36.59ms","37.59ms","38.59ms","39.59ms",    "40.59ms","41.59ms","42.59ms","43.59ms","44.59ms","45.59ms","46.59ms","47.59ms","48.59ms","49.59ms",    "50.59ms","51.59ms","52.59ms","53.59ms","54.59ms","55.59ms","56.59ms","57.59ms","58.59ms","59.59ms",    "60.59ms","61.59ms","62.59ms","63.59ms","64.59ms","65.59ms","66.59ms","67.59ms","68.59ms","69.59ms",    "70.59ms","71.59ms","72.59ms","73.59ms","74.59ms","75.59ms","76.59ms","77.59ms","78.59ms","79.59ms",    "80.59ms","81.59ms","82.59ms","83.59ms","84.59ms","85.59ms","86.59ms","87.59ms","88.59ms","89.59ms",    "90.59ms","91.59ms","92.59ms","93.59ms","94.59ms","95.59ms","96.59ms","97.59ms","98.59ms","99.59ms", },
      new string[] { "0.60ms","1.60ms","2.60ms","3.60ms","4.60ms","5.60ms","6.60ms","7.60ms","8.60ms","9.60ms",    "10.60ms","11.60ms","12.60ms","13.60ms","14.60ms","15.60ms","16.60ms","17.60ms","18.60ms","19.60ms",    "20.60ms","21.60ms","22.60ms","23.60ms","24.60ms","25.60ms","26.60ms","27.60ms","28.60ms","29.60ms",    "30.60ms","31.60ms","32.60ms","33.60ms","34.60ms","35.60ms","36.60ms","37.60ms","38.60ms","39.60ms",    "40.60ms","41.60ms","42.60ms","43.60ms","44.60ms","45.60ms","46.60ms","47.60ms","48.60ms","49.60ms",    "50.60ms","51.60ms","52.60ms","53.60ms","54.60ms","55.60ms","56.60ms","57.60ms","58.60ms","59.60ms",    "60.60ms","61.60ms","62.60ms","63.60ms","64.60ms","65.60ms","66.60ms","67.60ms","68.60ms","69.60ms",    "70.60ms","71.60ms","72.60ms","73.60ms","74.60ms","75.60ms","76.60ms","77.60ms","78.60ms","79.60ms",    "80.60ms","81.60ms","82.60ms","83.60ms","84.60ms","85.60ms","86.60ms","87.60ms","88.60ms","89.60ms",    "90.60ms","91.60ms","92.60ms","93.60ms","94.60ms","95.60ms","96.60ms","97.60ms","98.60ms","99.60ms", },
      new string[] { "0.61ms","1.61ms","2.61ms","3.61ms","4.61ms","5.61ms","6.61ms","7.61ms","8.61ms","9.61ms",    "10.61ms","11.61ms","12.61ms","13.61ms","14.61ms","15.61ms","16.61ms","17.61ms","18.61ms","19.61ms",    "20.61ms","21.61ms","22.61ms","23.61ms","24.61ms","25.61ms","26.61ms","27.61ms","28.61ms","29.61ms",    "30.61ms","31.61ms","32.61ms","33.61ms","34.61ms","35.61ms","36.61ms","37.61ms","38.61ms","39.61ms",    "40.61ms","41.61ms","42.61ms","43.61ms","44.61ms","45.61ms","46.61ms","47.61ms","48.61ms","49.61ms",    "50.61ms","51.61ms","52.61ms","53.61ms","54.61ms","55.61ms","56.61ms","57.61ms","58.61ms","59.61ms",    "60.61ms","61.61ms","62.61ms","63.61ms","64.61ms","65.61ms","66.61ms","67.61ms","68.61ms","69.61ms",    "70.61ms","71.61ms","72.61ms","73.61ms","74.61ms","75.61ms","76.61ms","77.61ms","78.61ms","79.61ms",    "80.61ms","81.61ms","82.61ms","83.61ms","84.61ms","85.61ms","86.61ms","87.61ms","88.61ms","89.61ms",    "90.61ms","91.61ms","92.61ms","93.61ms","94.61ms","95.61ms","96.61ms","97.61ms","98.61ms","99.61ms", },
      new string[] { "0.62ms","1.62ms","2.62ms","3.62ms","4.62ms","5.62ms","6.62ms","7.62ms","8.62ms","9.62ms",    "10.62ms","11.62ms","12.62ms","13.62ms","14.62ms","15.62ms","16.62ms","17.62ms","18.62ms","19.62ms",    "20.62ms","21.62ms","22.62ms","23.62ms","24.62ms","25.62ms","26.62ms","27.62ms","28.62ms","29.62ms",    "30.62ms","31.62ms","32.62ms","33.62ms","34.62ms","35.62ms","36.62ms","37.62ms","38.62ms","39.62ms",    "40.62ms","41.62ms","42.62ms","43.62ms","44.62ms","45.62ms","46.62ms","47.62ms","48.62ms","49.62ms",    "50.62ms","51.62ms","52.62ms","53.62ms","54.62ms","55.62ms","56.62ms","57.62ms","58.62ms","59.62ms",    "60.62ms","61.62ms","62.62ms","63.62ms","64.62ms","65.62ms","66.62ms","67.62ms","68.62ms","69.62ms",    "70.62ms","71.62ms","72.62ms","73.62ms","74.62ms","75.62ms","76.62ms","77.62ms","78.62ms","79.62ms",    "80.62ms","81.62ms","82.62ms","83.62ms","84.62ms","85.62ms","86.62ms","87.62ms","88.62ms","89.62ms",    "90.62ms","91.62ms","92.62ms","93.62ms","94.62ms","95.62ms","96.62ms","97.62ms","98.62ms","99.62ms", },
      new string[] { "0.63ms","1.63ms","2.63ms","3.63ms","4.63ms","5.63ms","6.63ms","7.63ms","8.63ms","9.63ms",    "10.63ms","11.63ms","12.63ms","13.63ms","14.63ms","15.63ms","16.63ms","17.63ms","18.63ms","19.63ms",    "20.63ms","21.63ms","22.63ms","23.63ms","24.63ms","25.63ms","26.63ms","27.63ms","28.63ms","29.63ms",    "30.63ms","31.63ms","32.63ms","33.63ms","34.63ms","35.63ms","36.63ms","37.63ms","38.63ms","39.63ms",    "40.63ms","41.63ms","42.63ms","43.63ms","44.63ms","45.63ms","46.63ms","47.63ms","48.63ms","49.63ms",    "50.63ms","51.63ms","52.63ms","53.63ms","54.63ms","55.63ms","56.63ms","57.63ms","58.63ms","59.63ms",    "60.63ms","61.63ms","62.63ms","63.63ms","64.63ms","65.63ms","66.63ms","67.63ms","68.63ms","69.63ms",    "70.63ms","71.63ms","72.63ms","73.63ms","74.63ms","75.63ms","76.63ms","77.63ms","78.63ms","79.63ms",    "80.63ms","81.63ms","82.63ms","83.63ms","84.63ms","85.63ms","86.63ms","87.63ms","88.63ms","89.63ms",    "90.63ms","91.63ms","92.63ms","93.63ms","94.63ms","95.63ms","96.63ms","97.63ms","98.63ms","99.63ms", },
      new string[] { "0.64ms","1.64ms","2.64ms","3.64ms","4.64ms","5.64ms","6.64ms","7.64ms","8.64ms","9.64ms",    "10.64ms","11.64ms","12.64ms","13.64ms","14.64ms","15.64ms","16.64ms","17.64ms","18.64ms","19.64ms",    "20.64ms","21.64ms","22.64ms","23.64ms","24.64ms","25.64ms","26.64ms","27.64ms","28.64ms","29.64ms",    "30.64ms","31.64ms","32.64ms","33.64ms","34.64ms","35.64ms","36.64ms","37.64ms","38.64ms","39.64ms",    "40.64ms","41.64ms","42.64ms","43.64ms","44.64ms","45.64ms","46.64ms","47.64ms","48.64ms","49.64ms",    "50.64ms","51.64ms","52.64ms","53.64ms","54.64ms","55.64ms","56.64ms","57.64ms","58.64ms","59.64ms",    "60.64ms","61.64ms","62.64ms","63.64ms","64.64ms","65.64ms","66.64ms","67.64ms","68.64ms","69.64ms",    "70.64ms","71.64ms","72.64ms","73.64ms","74.64ms","75.64ms","76.64ms","77.64ms","78.64ms","79.64ms",    "80.64ms","81.64ms","82.64ms","83.64ms","84.64ms","85.64ms","86.64ms","87.64ms","88.64ms","89.64ms",    "90.64ms","91.64ms","92.64ms","93.64ms","94.64ms","95.64ms","96.64ms","97.64ms","98.64ms","99.64ms", },
      new string[] { "0.65ms","1.65ms","2.65ms","3.65ms","4.65ms","5.65ms","6.65ms","7.65ms","8.65ms","9.65ms",    "10.65ms","11.65ms","12.65ms","13.65ms","14.65ms","15.65ms","16.65ms","17.65ms","18.65ms","19.65ms",    "20.65ms","21.65ms","22.65ms","23.65ms","24.65ms","25.65ms","26.65ms","27.65ms","28.65ms","29.65ms",    "30.65ms","31.65ms","32.65ms","33.65ms","34.65ms","35.65ms","36.65ms","37.65ms","38.65ms","39.65ms",    "40.65ms","41.65ms","42.65ms","43.65ms","44.65ms","45.65ms","46.65ms","47.65ms","48.65ms","49.65ms",    "50.65ms","51.65ms","52.65ms","53.65ms","54.65ms","55.65ms","56.65ms","57.65ms","58.65ms","59.65ms",    "60.65ms","61.65ms","62.65ms","63.65ms","64.65ms","65.65ms","66.65ms","67.65ms","68.65ms","69.65ms",    "70.65ms","71.65ms","72.65ms","73.65ms","74.65ms","75.65ms","76.65ms","77.65ms","78.65ms","79.65ms",    "80.65ms","81.65ms","82.65ms","83.65ms","84.65ms","85.65ms","86.65ms","87.65ms","88.65ms","89.65ms",    "90.65ms","91.65ms","92.65ms","93.65ms","94.65ms","95.65ms","96.65ms","97.65ms","98.65ms","99.65ms", },
      new string[] { "0.66ms","1.66ms","2.66ms","3.66ms","4.66ms","5.66ms","6.66ms","7.66ms","8.66ms","9.66ms",    "10.66ms","11.66ms","12.66ms","13.66ms","14.66ms","15.66ms","16.66ms","17.66ms","18.66ms","19.66ms",    "20.66ms","21.66ms","22.66ms","23.66ms","24.66ms","25.66ms","26.66ms","27.66ms","28.66ms","29.66ms",    "30.66ms","31.66ms","32.66ms","33.66ms","34.66ms","35.66ms","36.66ms","37.66ms","38.66ms","39.66ms",    "40.66ms","41.66ms","42.66ms","43.66ms","44.66ms","45.66ms","46.66ms","47.66ms","48.66ms","49.66ms",    "50.66ms","51.66ms","52.66ms","53.66ms","54.66ms","55.66ms","56.66ms","57.66ms","58.66ms","59.66ms",    "60.66ms","61.66ms","62.66ms","63.66ms","64.66ms","65.66ms","66.66ms","67.66ms","68.66ms","69.66ms",    "70.66ms","71.66ms","72.66ms","73.66ms","74.66ms","75.66ms","76.66ms","77.66ms","78.66ms","79.66ms",    "80.66ms","81.66ms","82.66ms","83.66ms","84.66ms","85.66ms","86.66ms","87.66ms","88.66ms","89.66ms",    "90.66ms","91.66ms","92.66ms","93.66ms","94.66ms","95.66ms","96.66ms","97.66ms","98.66ms","99.66ms", },
      new string[] { "0.67ms","1.67ms","2.67ms","3.67ms","4.67ms","5.67ms","6.67ms","7.67ms","8.67ms","9.67ms",    "10.67ms","11.67ms","12.67ms","13.67ms","14.67ms","15.67ms","16.67ms","17.67ms","18.67ms","19.67ms",    "20.67ms","21.67ms","22.67ms","23.67ms","24.67ms","25.67ms","26.67ms","27.67ms","28.67ms","29.67ms",    "30.67ms","31.67ms","32.67ms","33.67ms","34.67ms","35.67ms","36.67ms","37.67ms","38.67ms","39.67ms",    "40.67ms","41.67ms","42.67ms","43.67ms","44.67ms","45.67ms","46.67ms","47.67ms","48.67ms","49.67ms",    "50.67ms","51.67ms","52.67ms","53.67ms","54.67ms","55.67ms","56.67ms","57.67ms","58.67ms","59.67ms",    "60.67ms","61.67ms","62.67ms","63.67ms","64.67ms","65.67ms","66.67ms","67.67ms","68.67ms","69.67ms",    "70.67ms","71.67ms","72.67ms","73.67ms","74.67ms","75.67ms","76.67ms","77.67ms","78.67ms","79.67ms",    "80.67ms","81.67ms","82.67ms","83.67ms","84.67ms","85.67ms","86.67ms","87.67ms","88.67ms","89.67ms",    "90.67ms","91.67ms","92.67ms","93.67ms","94.67ms","95.67ms","96.67ms","97.67ms","98.67ms","99.67ms", },
      new string[] { "0.68ms","1.68ms","2.68ms","3.68ms","4.68ms","5.68ms","6.68ms","7.68ms","8.68ms","9.68ms",    "10.68ms","11.68ms","12.68ms","13.68ms","14.68ms","15.68ms","16.68ms","17.68ms","18.68ms","19.68ms",    "20.68ms","21.68ms","22.68ms","23.68ms","24.68ms","25.68ms","26.68ms","27.68ms","28.68ms","29.68ms",    "30.68ms","31.68ms","32.68ms","33.68ms","34.68ms","35.68ms","36.68ms","37.68ms","38.68ms","39.68ms",    "40.68ms","41.68ms","42.68ms","43.68ms","44.68ms","45.68ms","46.68ms","47.68ms","48.68ms","49.68ms",    "50.68ms","51.68ms","52.68ms","53.68ms","54.68ms","55.68ms","56.68ms","57.68ms","58.68ms","59.68ms",    "60.68ms","61.68ms","62.68ms","63.68ms","64.68ms","65.68ms","66.68ms","67.68ms","68.68ms","69.68ms",    "70.68ms","71.68ms","72.68ms","73.68ms","74.68ms","75.68ms","76.68ms","77.68ms","78.68ms","79.68ms",    "80.68ms","81.68ms","82.68ms","83.68ms","84.68ms","85.68ms","86.68ms","87.68ms","88.68ms","89.68ms",    "90.68ms","91.68ms","92.68ms","93.68ms","94.68ms","95.68ms","96.68ms","97.68ms","98.68ms","99.68ms", },
      new string[] { "0.69ms","1.69ms","2.69ms","3.69ms","4.69ms","5.69ms","6.69ms","7.69ms","8.69ms","9.69ms",    "10.69ms","11.69ms","12.69ms","13.69ms","14.69ms","15.69ms","16.69ms","17.69ms","18.69ms","19.69ms",    "20.69ms","21.69ms","22.69ms","23.69ms","24.69ms","25.69ms","26.69ms","27.69ms","28.69ms","29.69ms",    "30.69ms","31.69ms","32.69ms","33.69ms","34.69ms","35.69ms","36.69ms","37.69ms","38.69ms","39.69ms",    "40.69ms","41.69ms","42.69ms","43.69ms","44.69ms","45.69ms","46.69ms","47.69ms","48.69ms","49.69ms",    "50.69ms","51.69ms","52.69ms","53.69ms","54.69ms","55.69ms","56.69ms","57.69ms","58.69ms","59.69ms",    "60.69ms","61.69ms","62.69ms","63.69ms","64.69ms","65.69ms","66.69ms","67.69ms","68.69ms","69.69ms",    "70.69ms","71.69ms","72.69ms","73.69ms","74.69ms","75.69ms","76.69ms","77.69ms","78.69ms","79.69ms",    "80.69ms","81.69ms","82.69ms","83.69ms","84.69ms","85.69ms","86.69ms","87.69ms","88.69ms","89.69ms",    "90.69ms","91.69ms","92.69ms","93.69ms","94.69ms","95.69ms","96.69ms","97.69ms","98.69ms","99.69ms", },
      new string[] { "0.70ms","1.70ms","2.70ms","3.70ms","4.70ms","5.70ms","6.70ms","7.70ms","8.70ms","9.70ms",    "10.70ms","11.70ms","12.70ms","13.70ms","14.70ms","15.70ms","16.70ms","17.70ms","18.70ms","19.70ms",    "20.70ms","21.70ms","22.70ms","23.70ms","24.70ms","25.70ms","26.70ms","27.70ms","28.70ms","29.70ms",    "30.70ms","31.70ms","32.70ms","33.70ms","34.70ms","35.70ms","36.70ms","37.70ms","38.70ms","39.70ms",    "40.70ms","41.70ms","42.70ms","43.70ms","44.70ms","45.70ms","46.70ms","47.70ms","48.70ms","49.70ms",    "50.70ms","51.70ms","52.70ms","53.70ms","54.70ms","55.70ms","56.70ms","57.70ms","58.70ms","59.70ms",    "60.70ms","61.70ms","62.70ms","63.70ms","64.70ms","65.70ms","66.70ms","67.70ms","68.70ms","69.70ms",    "70.70ms","71.70ms","72.70ms","73.70ms","74.70ms","75.70ms","76.70ms","77.70ms","78.70ms","79.70ms",    "80.70ms","81.70ms","82.70ms","83.70ms","84.70ms","85.70ms","86.70ms","87.70ms","88.70ms","89.70ms",    "90.70ms","91.70ms","92.70ms","93.70ms","94.70ms","95.70ms","96.70ms","97.70ms","98.70ms","99.70ms", },
      new string[] { "0.71ms","1.71ms","2.71ms","3.71ms","4.71ms","5.71ms","6.71ms","7.71ms","8.71ms","9.71ms",    "10.71ms","11.71ms","12.71ms","13.71ms","14.71ms","15.71ms","16.71ms","17.71ms","18.71ms","19.71ms",    "20.71ms","21.71ms","22.71ms","23.71ms","24.71ms","25.71ms","26.71ms","27.71ms","28.71ms","29.71ms",    "30.71ms","31.71ms","32.71ms","33.71ms","34.71ms","35.71ms","36.71ms","37.71ms","38.71ms","39.71ms",    "40.71ms","41.71ms","42.71ms","43.71ms","44.71ms","45.71ms","46.71ms","47.71ms","48.71ms","49.71ms",    "50.71ms","51.71ms","52.71ms","53.71ms","54.71ms","55.71ms","56.71ms","57.71ms","58.71ms","59.71ms",    "60.71ms","61.71ms","62.71ms","63.71ms","64.71ms","65.71ms","66.71ms","67.71ms","68.71ms","69.71ms",    "70.71ms","71.71ms","72.71ms","73.71ms","74.71ms","75.71ms","76.71ms","77.71ms","78.71ms","79.71ms",    "80.71ms","81.71ms","82.71ms","83.71ms","84.71ms","85.71ms","86.71ms","87.71ms","88.71ms","89.71ms",    "90.71ms","91.71ms","92.71ms","93.71ms","94.71ms","95.71ms","96.71ms","97.71ms","98.71ms","99.71ms", },
      new string[] { "0.72ms","1.72ms","2.72ms","3.72ms","4.72ms","5.72ms","6.72ms","7.72ms","8.72ms","9.72ms",    "10.72ms","11.72ms","12.72ms","13.72ms","14.72ms","15.72ms","16.72ms","17.72ms","18.72ms","19.72ms",    "20.72ms","21.72ms","22.72ms","23.72ms","24.72ms","25.72ms","26.72ms","27.72ms","28.72ms","29.72ms",    "30.72ms","31.72ms","32.72ms","33.72ms","34.72ms","35.72ms","36.72ms","37.72ms","38.72ms","39.72ms",    "40.72ms","41.72ms","42.72ms","43.72ms","44.72ms","45.72ms","46.72ms","47.72ms","48.72ms","49.72ms",    "50.72ms","51.72ms","52.72ms","53.72ms","54.72ms","55.72ms","56.72ms","57.72ms","58.72ms","59.72ms",    "60.72ms","61.72ms","62.72ms","63.72ms","64.72ms","65.72ms","66.72ms","67.72ms","68.72ms","69.72ms",    "70.72ms","71.72ms","72.72ms","73.72ms","74.72ms","75.72ms","76.72ms","77.72ms","78.72ms","79.72ms",    "80.72ms","81.72ms","82.72ms","83.72ms","84.72ms","85.72ms","86.72ms","87.72ms","88.72ms","89.72ms",    "90.72ms","91.72ms","92.72ms","93.72ms","94.72ms","95.72ms","96.72ms","97.72ms","98.72ms","99.72ms", },
      new string[] { "0.73ms","1.73ms","2.73ms","3.73ms","4.73ms","5.73ms","6.73ms","7.73ms","8.73ms","9.73ms",    "10.73ms","11.73ms","12.73ms","13.73ms","14.73ms","15.73ms","16.73ms","17.73ms","18.73ms","19.73ms",    "20.73ms","21.73ms","22.73ms","23.73ms","24.73ms","25.73ms","26.73ms","27.73ms","28.73ms","29.73ms",    "30.73ms","31.73ms","32.73ms","33.73ms","34.73ms","35.73ms","36.73ms","37.73ms","38.73ms","39.73ms",    "40.73ms","41.73ms","42.73ms","43.73ms","44.73ms","45.73ms","46.73ms","47.73ms","48.73ms","49.73ms",    "50.73ms","51.73ms","52.73ms","53.73ms","54.73ms","55.73ms","56.73ms","57.73ms","58.73ms","59.73ms",    "60.73ms","61.73ms","62.73ms","63.73ms","64.73ms","65.73ms","66.73ms","67.73ms","68.73ms","69.73ms",    "70.73ms","71.73ms","72.73ms","73.73ms","74.73ms","75.73ms","76.73ms","77.73ms","78.73ms","79.73ms",    "80.73ms","81.73ms","82.73ms","83.73ms","84.73ms","85.73ms","86.73ms","87.73ms","88.73ms","89.73ms",    "90.73ms","91.73ms","92.73ms","93.73ms","94.73ms","95.73ms","96.73ms","97.73ms","98.73ms","99.73ms", },
      new string[] { "0.74ms","1.74ms","2.74ms","3.74ms","4.74ms","5.74ms","6.74ms","7.74ms","8.74ms","9.74ms",    "10.74ms","11.74ms","12.74ms","13.74ms","14.74ms","15.74ms","16.74ms","17.74ms","18.74ms","19.74ms",    "20.74ms","21.74ms","22.74ms","23.74ms","24.74ms","25.74ms","26.74ms","27.74ms","28.74ms","29.74ms",    "30.74ms","31.74ms","32.74ms","33.74ms","34.74ms","35.74ms","36.74ms","37.74ms","38.74ms","39.74ms",    "40.74ms","41.74ms","42.74ms","43.74ms","44.74ms","45.74ms","46.74ms","47.74ms","48.74ms","49.74ms",    "50.74ms","51.74ms","52.74ms","53.74ms","54.74ms","55.74ms","56.74ms","57.74ms","58.74ms","59.74ms",    "60.74ms","61.74ms","62.74ms","63.74ms","64.74ms","65.74ms","66.74ms","67.74ms","68.74ms","69.74ms",    "70.74ms","71.74ms","72.74ms","73.74ms","74.74ms","75.74ms","76.74ms","77.74ms","78.74ms","79.74ms",    "80.74ms","81.74ms","82.74ms","83.74ms","84.74ms","85.74ms","86.74ms","87.74ms","88.74ms","89.74ms",    "90.74ms","91.74ms","92.74ms","93.74ms","94.74ms","95.74ms","96.74ms","97.74ms","98.74ms","99.74ms", },
      new string[] { "0.75ms","1.75ms","2.75ms","3.75ms","4.75ms","5.75ms","6.75ms","7.75ms","8.75ms","9.75ms",    "10.75ms","11.75ms","12.75ms","13.75ms","14.75ms","15.75ms","16.75ms","17.75ms","18.75ms","19.75ms",    "20.75ms","21.75ms","22.75ms","23.75ms","24.75ms","25.75ms","26.75ms","27.75ms","28.75ms","29.75ms",    "30.75ms","31.75ms","32.75ms","33.75ms","34.75ms","35.75ms","36.75ms","37.75ms","38.75ms","39.75ms",    "40.75ms","41.75ms","42.75ms","43.75ms","44.75ms","45.75ms","46.75ms","47.75ms","48.75ms","49.75ms",    "50.75ms","51.75ms","52.75ms","53.75ms","54.75ms","55.75ms","56.75ms","57.75ms","58.75ms","59.75ms",    "60.75ms","61.75ms","62.75ms","63.75ms","64.75ms","65.75ms","66.75ms","67.75ms","68.75ms","69.75ms",    "70.75ms","71.75ms","72.75ms","73.75ms","74.75ms","75.75ms","76.75ms","77.75ms","78.75ms","79.75ms",    "80.75ms","81.75ms","82.75ms","83.75ms","84.75ms","85.75ms","86.75ms","87.75ms","88.75ms","89.75ms",    "90.75ms","91.75ms","92.75ms","93.75ms","94.75ms","95.75ms","96.75ms","97.75ms","98.75ms","99.75ms", },
      new string[] { "0.76ms","1.76ms","2.76ms","3.76ms","4.76ms","5.76ms","6.76ms","7.76ms","8.76ms","9.76ms",    "10.76ms","11.76ms","12.76ms","13.76ms","14.76ms","15.76ms","16.76ms","17.76ms","18.76ms","19.76ms",    "20.76ms","21.76ms","22.76ms","23.76ms","24.76ms","25.76ms","26.76ms","27.76ms","28.76ms","29.76ms",    "30.76ms","31.76ms","32.76ms","33.76ms","34.76ms","35.76ms","36.76ms","37.76ms","38.76ms","39.76ms",    "40.76ms","41.76ms","42.76ms","43.76ms","44.76ms","45.76ms","46.76ms","47.76ms","48.76ms","49.76ms",    "50.76ms","51.76ms","52.76ms","53.76ms","54.76ms","55.76ms","56.76ms","57.76ms","58.76ms","59.76ms",    "60.76ms","61.76ms","62.76ms","63.76ms","64.76ms","65.76ms","66.76ms","67.76ms","68.76ms","69.76ms",    "70.76ms","71.76ms","72.76ms","73.76ms","74.76ms","75.76ms","76.76ms","77.76ms","78.76ms","79.76ms",    "80.76ms","81.76ms","82.76ms","83.76ms","84.76ms","85.76ms","86.76ms","87.76ms","88.76ms","89.76ms",    "90.76ms","91.76ms","92.76ms","93.76ms","94.76ms","95.76ms","96.76ms","97.76ms","98.76ms","99.76ms", },
      new string[] { "0.77ms","1.77ms","2.77ms","3.77ms","4.77ms","5.77ms","6.77ms","7.77ms","8.77ms","9.77ms",    "10.77ms","11.77ms","12.77ms","13.77ms","14.77ms","15.77ms","16.77ms","17.77ms","18.77ms","19.77ms",    "20.77ms","21.77ms","22.77ms","23.77ms","24.77ms","25.77ms","26.77ms","27.77ms","28.77ms","29.77ms",    "30.77ms","31.77ms","32.77ms","33.77ms","34.77ms","35.77ms","36.77ms","37.77ms","38.77ms","39.77ms",    "40.77ms","41.77ms","42.77ms","43.77ms","44.77ms","45.77ms","46.77ms","47.77ms","48.77ms","49.77ms",    "50.77ms","51.77ms","52.77ms","53.77ms","54.77ms","55.77ms","56.77ms","57.77ms","58.77ms","59.77ms",    "60.77ms","61.77ms","62.77ms","63.77ms","64.77ms","65.77ms","66.77ms","67.77ms","68.77ms","69.77ms",    "70.77ms","71.77ms","72.77ms","73.77ms","74.77ms","75.77ms","76.77ms","77.77ms","78.77ms","79.77ms",    "80.77ms","81.77ms","82.77ms","83.77ms","84.77ms","85.77ms","86.77ms","87.77ms","88.77ms","89.77ms",    "90.77ms","91.77ms","92.77ms","93.77ms","94.77ms","95.77ms","96.77ms","97.77ms","98.77ms","99.77ms", },
      new string[] { "0.78ms","1.78ms","2.78ms","3.78ms","4.78ms","5.78ms","6.78ms","7.78ms","8.78ms","9.78ms",    "10.78ms","11.78ms","12.78ms","13.78ms","14.78ms","15.78ms","16.78ms","17.78ms","18.78ms","19.78ms",    "20.78ms","21.78ms","22.78ms","23.78ms","24.78ms","25.78ms","26.78ms","27.78ms","28.78ms","29.78ms",    "30.78ms","31.78ms","32.78ms","33.78ms","34.78ms","35.78ms","36.78ms","37.78ms","38.78ms","39.78ms",    "40.78ms","41.78ms","42.78ms","43.78ms","44.78ms","45.78ms","46.78ms","47.78ms","48.78ms","49.78ms",    "50.78ms","51.78ms","52.78ms","53.78ms","54.78ms","55.78ms","56.78ms","57.78ms","58.78ms","59.78ms",    "60.78ms","61.78ms","62.78ms","63.78ms","64.78ms","65.78ms","66.78ms","67.78ms","68.78ms","69.78ms",    "70.78ms","71.78ms","72.78ms","73.78ms","74.78ms","75.78ms","76.78ms","77.78ms","78.78ms","79.78ms",    "80.78ms","81.78ms","82.78ms","83.78ms","84.78ms","85.78ms","86.78ms","87.78ms","88.78ms","89.78ms",    "90.78ms","91.78ms","92.78ms","93.78ms","94.78ms","95.78ms","96.78ms","97.78ms","98.78ms","99.78ms", },
      new string[] { "0.79ms","1.79ms","2.79ms","3.79ms","4.79ms","5.79ms","6.79ms","7.79ms","8.79ms","9.79ms",    "10.79ms","11.79ms","12.79ms","13.79ms","14.79ms","15.79ms","16.79ms","17.79ms","18.79ms","19.79ms",    "20.79ms","21.79ms","22.79ms","23.79ms","24.79ms","25.79ms","26.79ms","27.79ms","28.79ms","29.79ms",    "30.79ms","31.79ms","32.79ms","33.79ms","34.79ms","35.79ms","36.79ms","37.79ms","38.79ms","39.79ms",    "40.79ms","41.79ms","42.79ms","43.79ms","44.79ms","45.79ms","46.79ms","47.79ms","48.79ms","49.79ms",    "50.79ms","51.79ms","52.79ms","53.79ms","54.79ms","55.79ms","56.79ms","57.79ms","58.79ms","59.79ms",    "60.79ms","61.79ms","62.79ms","63.79ms","64.79ms","65.79ms","66.79ms","67.79ms","68.79ms","69.79ms",    "70.79ms","71.79ms","72.79ms","73.79ms","74.79ms","75.79ms","76.79ms","77.79ms","78.79ms","79.79ms",    "80.79ms","81.79ms","82.79ms","83.79ms","84.79ms","85.79ms","86.79ms","87.79ms","88.79ms","89.79ms",    "90.79ms","91.79ms","92.79ms","93.79ms","94.79ms","95.79ms","96.79ms","97.79ms","98.79ms","99.79ms", },
      new string[] { "0.80ms","1.80ms","2.80ms","3.80ms","4.80ms","5.80ms","6.80ms","7.80ms","8.80ms","9.80ms",    "10.80ms","11.80ms","12.80ms","13.80ms","14.80ms","15.80ms","16.80ms","17.80ms","18.80ms","19.80ms",    "20.80ms","21.80ms","22.80ms","23.80ms","24.80ms","25.80ms","26.80ms","27.80ms","28.80ms","29.80ms",    "30.80ms","31.80ms","32.80ms","33.80ms","34.80ms","35.80ms","36.80ms","37.80ms","38.80ms","39.80ms",    "40.80ms","41.80ms","42.80ms","43.80ms","44.80ms","45.80ms","46.80ms","47.80ms","48.80ms","49.80ms",    "50.80ms","51.80ms","52.80ms","53.80ms","54.80ms","55.80ms","56.80ms","57.80ms","58.80ms","59.80ms",    "60.80ms","61.80ms","62.80ms","63.80ms","64.80ms","65.80ms","66.80ms","67.80ms","68.80ms","69.80ms",    "70.80ms","71.80ms","72.80ms","73.80ms","74.80ms","75.80ms","76.80ms","77.80ms","78.80ms","79.80ms",    "80.80ms","81.80ms","82.80ms","83.80ms","84.80ms","85.80ms","86.80ms","87.80ms","88.80ms","89.80ms",    "90.80ms","91.80ms","92.80ms","93.80ms","94.80ms","95.80ms","96.80ms","97.80ms","98.80ms","99.80ms", },
      new string[] { "0.81ms","1.81ms","2.81ms","3.81ms","4.81ms","5.81ms","6.81ms","7.81ms","8.81ms","9.81ms",    "10.81ms","11.81ms","12.81ms","13.81ms","14.81ms","15.81ms","16.81ms","17.81ms","18.81ms","19.81ms",    "20.81ms","21.81ms","22.81ms","23.81ms","24.81ms","25.81ms","26.81ms","27.81ms","28.81ms","29.81ms",    "30.81ms","31.81ms","32.81ms","33.81ms","34.81ms","35.81ms","36.81ms","37.81ms","38.81ms","39.81ms",    "40.81ms","41.81ms","42.81ms","43.81ms","44.81ms","45.81ms","46.81ms","47.81ms","48.81ms","49.81ms",    "50.81ms","51.81ms","52.81ms","53.81ms","54.81ms","55.81ms","56.81ms","57.81ms","58.81ms","59.81ms",    "60.81ms","61.81ms","62.81ms","63.81ms","64.81ms","65.81ms","66.81ms","67.81ms","68.81ms","69.81ms",    "70.81ms","71.81ms","72.81ms","73.81ms","74.81ms","75.81ms","76.81ms","77.81ms","78.81ms","79.81ms",    "80.81ms","81.81ms","82.81ms","83.81ms","84.81ms","85.81ms","86.81ms","87.81ms","88.81ms","89.81ms",    "90.81ms","91.81ms","92.81ms","93.81ms","94.81ms","95.81ms","96.81ms","97.81ms","98.81ms","99.81ms", },
      new string[] { "0.82ms","1.82ms","2.82ms","3.82ms","4.82ms","5.82ms","6.82ms","7.82ms","8.82ms","9.82ms",    "10.82ms","11.82ms","12.82ms","13.82ms","14.82ms","15.82ms","16.82ms","17.82ms","18.82ms","19.82ms",    "20.82ms","21.82ms","22.82ms","23.82ms","24.82ms","25.82ms","26.82ms","27.82ms","28.82ms","29.82ms",    "30.82ms","31.82ms","32.82ms","33.82ms","34.82ms","35.82ms","36.82ms","37.82ms","38.82ms","39.82ms",    "40.82ms","41.82ms","42.82ms","43.82ms","44.82ms","45.82ms","46.82ms","47.82ms","48.82ms","49.82ms",    "50.82ms","51.82ms","52.82ms","53.82ms","54.82ms","55.82ms","56.82ms","57.82ms","58.82ms","59.82ms",    "60.82ms","61.82ms","62.82ms","63.82ms","64.82ms","65.82ms","66.82ms","67.82ms","68.82ms","69.82ms",    "70.82ms","71.82ms","72.82ms","73.82ms","74.82ms","75.82ms","76.82ms","77.82ms","78.82ms","79.82ms",    "80.82ms","81.82ms","82.82ms","83.82ms","84.82ms","85.82ms","86.82ms","87.82ms","88.82ms","89.82ms",    "90.82ms","91.82ms","92.82ms","93.82ms","94.82ms","95.82ms","96.82ms","97.82ms","98.82ms","99.82ms", },
      new string[] { "0.83ms","1.83ms","2.83ms","3.83ms","4.83ms","5.83ms","6.83ms","7.83ms","8.83ms","9.83ms",    "10.83ms","11.83ms","12.83ms","13.83ms","14.83ms","15.83ms","16.83ms","17.83ms","18.83ms","19.83ms",    "20.83ms","21.83ms","22.83ms","23.83ms","24.83ms","25.83ms","26.83ms","27.83ms","28.83ms","29.83ms",    "30.83ms","31.83ms","32.83ms","33.83ms","34.83ms","35.83ms","36.83ms","37.83ms","38.83ms","39.83ms",    "40.83ms","41.83ms","42.83ms","43.83ms","44.83ms","45.83ms","46.83ms","47.83ms","48.83ms","49.83ms",    "50.83ms","51.83ms","52.83ms","53.83ms","54.83ms","55.83ms","56.83ms","57.83ms","58.83ms","59.83ms",    "60.83ms","61.83ms","62.83ms","63.83ms","64.83ms","65.83ms","66.83ms","67.83ms","68.83ms","69.83ms",    "70.83ms","71.83ms","72.83ms","73.83ms","74.83ms","75.83ms","76.83ms","77.83ms","78.83ms","79.83ms",    "80.83ms","81.83ms","82.83ms","83.83ms","84.83ms","85.83ms","86.83ms","87.83ms","88.83ms","89.83ms",    "90.83ms","91.83ms","92.83ms","93.83ms","94.83ms","95.83ms","96.83ms","97.83ms","98.83ms","99.83ms", },
      new string[] { "0.84ms","1.84ms","2.84ms","3.84ms","4.84ms","5.84ms","6.84ms","7.84ms","8.84ms","9.84ms",    "10.84ms","11.84ms","12.84ms","13.84ms","14.84ms","15.84ms","16.84ms","17.84ms","18.84ms","19.84ms",    "20.84ms","21.84ms","22.84ms","23.84ms","24.84ms","25.84ms","26.84ms","27.84ms","28.84ms","29.84ms",    "30.84ms","31.84ms","32.84ms","33.84ms","34.84ms","35.84ms","36.84ms","37.84ms","38.84ms","39.84ms",    "40.84ms","41.84ms","42.84ms","43.84ms","44.84ms","45.84ms","46.84ms","47.84ms","48.84ms","49.84ms",    "50.84ms","51.84ms","52.84ms","53.84ms","54.84ms","55.84ms","56.84ms","57.84ms","58.84ms","59.84ms",    "60.84ms","61.84ms","62.84ms","63.84ms","64.84ms","65.84ms","66.84ms","67.84ms","68.84ms","69.84ms",    "70.84ms","71.84ms","72.84ms","73.84ms","74.84ms","75.84ms","76.84ms","77.84ms","78.84ms","79.84ms",    "80.84ms","81.84ms","82.84ms","83.84ms","84.84ms","85.84ms","86.84ms","87.84ms","88.84ms","89.84ms",    "90.84ms","91.84ms","92.84ms","93.84ms","94.84ms","95.84ms","96.84ms","97.84ms","98.84ms","99.84ms", },
      new string[] { "0.85ms","1.85ms","2.85ms","3.85ms","4.85ms","5.85ms","6.85ms","7.85ms","8.85ms","9.85ms",    "10.85ms","11.85ms","12.85ms","13.85ms","14.85ms","15.85ms","16.85ms","17.85ms","18.85ms","19.85ms",    "20.85ms","21.85ms","22.85ms","23.85ms","24.85ms","25.85ms","26.85ms","27.85ms","28.85ms","29.85ms",    "30.85ms","31.85ms","32.85ms","33.85ms","34.85ms","35.85ms","36.85ms","37.85ms","38.85ms","39.85ms",    "40.85ms","41.85ms","42.85ms","43.85ms","44.85ms","45.85ms","46.85ms","47.85ms","48.85ms","49.85ms",    "50.85ms","51.85ms","52.85ms","53.85ms","54.85ms","55.85ms","56.85ms","57.85ms","58.85ms","59.85ms",    "60.85ms","61.85ms","62.85ms","63.85ms","64.85ms","65.85ms","66.85ms","67.85ms","68.85ms","69.85ms",    "70.85ms","71.85ms","72.85ms","73.85ms","74.85ms","75.85ms","76.85ms","77.85ms","78.85ms","79.85ms",    "80.85ms","81.85ms","82.85ms","83.85ms","84.85ms","85.85ms","86.85ms","87.85ms","88.85ms","89.85ms",    "90.85ms","91.85ms","92.85ms","93.85ms","94.85ms","95.85ms","96.85ms","97.85ms","98.85ms","99.85ms", },
      new string[] { "0.86ms","1.86ms","2.86ms","3.86ms","4.86ms","5.86ms","6.86ms","7.86ms","8.86ms","9.86ms",    "10.86ms","11.86ms","12.86ms","13.86ms","14.86ms","15.86ms","16.86ms","17.86ms","18.86ms","19.86ms",    "20.86ms","21.86ms","22.86ms","23.86ms","24.86ms","25.86ms","26.86ms","27.86ms","28.86ms","29.86ms",    "30.86ms","31.86ms","32.86ms","33.86ms","34.86ms","35.86ms","36.86ms","37.86ms","38.86ms","39.86ms",    "40.86ms","41.86ms","42.86ms","43.86ms","44.86ms","45.86ms","46.86ms","47.86ms","48.86ms","49.86ms",    "50.86ms","51.86ms","52.86ms","53.86ms","54.86ms","55.86ms","56.86ms","57.86ms","58.86ms","59.86ms",    "60.86ms","61.86ms","62.86ms","63.86ms","64.86ms","65.86ms","66.86ms","67.86ms","68.86ms","69.86ms",    "70.86ms","71.86ms","72.86ms","73.86ms","74.86ms","75.86ms","76.86ms","77.86ms","78.86ms","79.86ms",    "80.86ms","81.86ms","82.86ms","83.86ms","84.86ms","85.86ms","86.86ms","87.86ms","88.86ms","89.86ms",    "90.86ms","91.86ms","92.86ms","93.86ms","94.86ms","95.86ms","96.86ms","97.86ms","98.86ms","99.86ms", },
      new string[] { "0.87ms","1.87ms","2.87ms","3.87ms","4.87ms","5.87ms","6.87ms","7.87ms","8.87ms","9.87ms",    "10.87ms","11.87ms","12.87ms","13.87ms","14.87ms","15.87ms","16.87ms","17.87ms","18.87ms","19.87ms",    "20.87ms","21.87ms","22.87ms","23.87ms","24.87ms","25.87ms","26.87ms","27.87ms","28.87ms","29.87ms",    "30.87ms","31.87ms","32.87ms","33.87ms","34.87ms","35.87ms","36.87ms","37.87ms","38.87ms","39.87ms",    "40.87ms","41.87ms","42.87ms","43.87ms","44.87ms","45.87ms","46.87ms","47.87ms","48.87ms","49.87ms",    "50.87ms","51.87ms","52.87ms","53.87ms","54.87ms","55.87ms","56.87ms","57.87ms","58.87ms","59.87ms",    "60.87ms","61.87ms","62.87ms","63.87ms","64.87ms","65.87ms","66.87ms","67.87ms","68.87ms","69.87ms",    "70.87ms","71.87ms","72.87ms","73.87ms","74.87ms","75.87ms","76.87ms","77.87ms","78.87ms","79.87ms",    "80.87ms","81.87ms","82.87ms","83.87ms","84.87ms","85.87ms","86.87ms","87.87ms","88.87ms","89.87ms",    "90.87ms","91.87ms","92.87ms","93.87ms","94.87ms","95.87ms","96.87ms","97.87ms","98.87ms","99.87ms", },
      new string[] { "0.88ms","1.88ms","2.88ms","3.88ms","4.88ms","5.88ms","6.88ms","7.88ms","8.88ms","9.88ms",    "10.88ms","11.88ms","12.88ms","13.88ms","14.88ms","15.88ms","16.88ms","17.88ms","18.88ms","19.88ms",    "20.88ms","21.88ms","22.88ms","23.88ms","24.88ms","25.88ms","26.88ms","27.88ms","28.88ms","29.88ms",    "30.88ms","31.88ms","32.88ms","33.88ms","34.88ms","35.88ms","36.88ms","37.88ms","38.88ms","39.88ms",    "40.88ms","41.88ms","42.88ms","43.88ms","44.88ms","45.88ms","46.88ms","47.88ms","48.88ms","49.88ms",    "50.88ms","51.88ms","52.88ms","53.88ms","54.88ms","55.88ms","56.88ms","57.88ms","58.88ms","59.88ms",    "60.88ms","61.88ms","62.88ms","63.88ms","64.88ms","65.88ms","66.88ms","67.88ms","68.88ms","69.88ms",    "70.88ms","71.88ms","72.88ms","73.88ms","74.88ms","75.88ms","76.88ms","77.88ms","78.88ms","79.88ms",    "80.88ms","81.88ms","82.88ms","83.88ms","84.88ms","85.88ms","86.88ms","87.88ms","88.88ms","89.88ms",    "90.88ms","91.88ms","92.88ms","93.88ms","94.88ms","95.88ms","96.88ms","97.88ms","98.88ms","99.88ms", },
      new string[] { "0.89ms","1.89ms","2.89ms","3.89ms","4.89ms","5.89ms","6.89ms","7.89ms","8.89ms","9.89ms",    "10.89ms","11.89ms","12.89ms","13.89ms","14.89ms","15.89ms","16.89ms","17.89ms","18.89ms","19.89ms",    "20.89ms","21.89ms","22.89ms","23.89ms","24.89ms","25.89ms","26.89ms","27.89ms","28.89ms","29.89ms",    "30.89ms","31.89ms","32.89ms","33.89ms","34.89ms","35.89ms","36.89ms","37.89ms","38.89ms","39.89ms",    "40.89ms","41.89ms","42.89ms","43.89ms","44.89ms","45.89ms","46.89ms","47.89ms","48.89ms","49.89ms",    "50.89ms","51.89ms","52.89ms","53.89ms","54.89ms","55.89ms","56.89ms","57.89ms","58.89ms","59.89ms",    "60.89ms","61.89ms","62.89ms","63.89ms","64.89ms","65.89ms","66.89ms","67.89ms","68.89ms","69.89ms",    "70.89ms","71.89ms","72.89ms","73.89ms","74.89ms","75.89ms","76.89ms","77.89ms","78.89ms","79.89ms",    "80.89ms","81.89ms","82.89ms","83.89ms","84.89ms","85.89ms","86.89ms","87.89ms","88.89ms","89.89ms",    "90.89ms","91.89ms","92.89ms","93.89ms","94.89ms","95.89ms","96.89ms","97.89ms","98.89ms","99.89ms", },
      new string[] { "0.90ms","1.90ms","2.90ms","3.90ms","4.90ms","5.90ms","6.90ms","7.90ms","8.90ms","9.90ms",    "10.90ms","11.90ms","12.90ms","13.90ms","14.90ms","15.90ms","16.90ms","17.90ms","18.90ms","19.90ms",    "20.90ms","21.90ms","22.90ms","23.90ms","24.90ms","25.90ms","26.90ms","27.90ms","28.90ms","29.90ms",    "30.90ms","31.90ms","32.90ms","33.90ms","34.90ms","35.90ms","36.90ms","37.90ms","38.90ms","39.90ms",    "40.90ms","41.90ms","42.90ms","43.90ms","44.90ms","45.90ms","46.90ms","47.90ms","48.90ms","49.90ms",    "50.90ms","51.90ms","52.90ms","53.90ms","54.90ms","55.90ms","56.90ms","57.90ms","58.90ms","59.90ms",    "60.90ms","61.90ms","62.90ms","63.90ms","64.90ms","65.90ms","66.90ms","67.90ms","68.90ms","69.90ms",    "70.90ms","71.90ms","72.90ms","73.90ms","74.90ms","75.90ms","76.90ms","77.90ms","78.90ms","79.90ms",    "80.90ms","81.90ms","82.90ms","83.90ms","84.90ms","85.90ms","86.90ms","87.90ms","88.90ms","89.90ms",    "90.90ms","91.90ms","92.90ms","93.90ms","94.90ms","95.90ms","96.90ms","97.90ms","98.90ms","99.90ms", },
      new string[] { "0.91ms","1.91ms","2.91ms","3.91ms","4.91ms","5.91ms","6.91ms","7.91ms","8.91ms","9.91ms",    "10.91ms","11.91ms","12.91ms","13.91ms","14.91ms","15.91ms","16.91ms","17.91ms","18.91ms","19.91ms",    "20.91ms","21.91ms","22.91ms","23.91ms","24.91ms","25.91ms","26.91ms","27.91ms","28.91ms","29.91ms",    "30.91ms","31.91ms","32.91ms","33.91ms","34.91ms","35.91ms","36.91ms","37.91ms","38.91ms","39.91ms",    "40.91ms","41.91ms","42.91ms","43.91ms","44.91ms","45.91ms","46.91ms","47.91ms","48.91ms","49.91ms",    "50.91ms","51.91ms","52.91ms","53.91ms","54.91ms","55.91ms","56.91ms","57.91ms","58.91ms","59.91ms",    "60.91ms","61.91ms","62.91ms","63.91ms","64.91ms","65.91ms","66.91ms","67.91ms","68.91ms","69.91ms",    "70.91ms","71.91ms","72.91ms","73.91ms","74.91ms","75.91ms","76.91ms","77.91ms","78.91ms","79.91ms",    "80.91ms","81.91ms","82.91ms","83.91ms","84.91ms","85.91ms","86.91ms","87.91ms","88.91ms","89.91ms",    "90.91ms","91.91ms","92.91ms","93.91ms","94.91ms","95.91ms","96.91ms","97.91ms","98.91ms","99.91ms", },
      new string[] { "0.92ms","1.92ms","2.92ms","3.92ms","4.92ms","5.92ms","6.92ms","7.92ms","8.92ms","9.92ms",    "10.92ms","11.92ms","12.92ms","13.92ms","14.92ms","15.92ms","16.92ms","17.92ms","18.92ms","19.92ms",    "20.92ms","21.92ms","22.92ms","23.92ms","24.92ms","25.92ms","26.92ms","27.92ms","28.92ms","29.92ms",    "30.92ms","31.92ms","32.92ms","33.92ms","34.92ms","35.92ms","36.92ms","37.92ms","38.92ms","39.92ms",    "40.92ms","41.92ms","42.92ms","43.92ms","44.92ms","45.92ms","46.92ms","47.92ms","48.92ms","49.92ms",    "50.92ms","51.92ms","52.92ms","53.92ms","54.92ms","55.92ms","56.92ms","57.92ms","58.92ms","59.92ms",    "60.92ms","61.92ms","62.92ms","63.92ms","64.92ms","65.92ms","66.92ms","67.92ms","68.92ms","69.92ms",    "70.92ms","71.92ms","72.92ms","73.92ms","74.92ms","75.92ms","76.92ms","77.92ms","78.92ms","79.92ms",    "80.92ms","81.92ms","82.92ms","83.92ms","84.92ms","85.92ms","86.92ms","87.92ms","88.92ms","89.92ms",    "90.92ms","91.92ms","92.92ms","93.92ms","94.92ms","95.92ms","96.92ms","97.92ms","98.92ms","99.92ms", },
      new string[] { "0.93ms","1.93ms","2.93ms","3.93ms","4.93ms","5.93ms","6.93ms","7.93ms","8.93ms","9.93ms",    "10.93ms","11.93ms","12.93ms","13.93ms","14.93ms","15.93ms","16.93ms","17.93ms","18.93ms","19.93ms",    "20.93ms","21.93ms","22.93ms","23.93ms","24.93ms","25.93ms","26.93ms","27.93ms","28.93ms","29.93ms",    "30.93ms","31.93ms","32.93ms","33.93ms","34.93ms","35.93ms","36.93ms","37.93ms","38.93ms","39.93ms",    "40.93ms","41.93ms","42.93ms","43.93ms","44.93ms","45.93ms","46.93ms","47.93ms","48.93ms","49.93ms",    "50.93ms","51.93ms","52.93ms","53.93ms","54.93ms","55.93ms","56.93ms","57.93ms","58.93ms","59.93ms",    "60.93ms","61.93ms","62.93ms","63.93ms","64.93ms","65.93ms","66.93ms","67.93ms","68.93ms","69.93ms",    "70.93ms","71.93ms","72.93ms","73.93ms","74.93ms","75.93ms","76.93ms","77.93ms","78.93ms","79.93ms",    "80.93ms","81.93ms","82.93ms","83.93ms","84.93ms","85.93ms","86.93ms","87.93ms","88.93ms","89.93ms",    "90.93ms","91.93ms","92.93ms","93.93ms","94.93ms","95.93ms","96.93ms","97.93ms","98.93ms","99.93ms", },
      new string[] { "0.94ms","1.94ms","2.94ms","3.94ms","4.94ms","5.94ms","6.94ms","7.94ms","8.94ms","9.94ms",    "10.94ms","11.94ms","12.94ms","13.94ms","14.94ms","15.94ms","16.94ms","17.94ms","18.94ms","19.94ms",    "20.94ms","21.94ms","22.94ms","23.94ms","24.94ms","25.94ms","26.94ms","27.94ms","28.94ms","29.94ms",    "30.94ms","31.94ms","32.94ms","33.94ms","34.94ms","35.94ms","36.94ms","37.94ms","38.94ms","39.94ms",    "40.94ms","41.94ms","42.94ms","43.94ms","44.94ms","45.94ms","46.94ms","47.94ms","48.94ms","49.94ms",    "50.94ms","51.94ms","52.94ms","53.94ms","54.94ms","55.94ms","56.94ms","57.94ms","58.94ms","59.94ms",    "60.94ms","61.94ms","62.94ms","63.94ms","64.94ms","65.94ms","66.94ms","67.94ms","68.94ms","69.94ms",    "70.94ms","71.94ms","72.94ms","73.94ms","74.94ms","75.94ms","76.94ms","77.94ms","78.94ms","79.94ms",    "80.94ms","81.94ms","82.94ms","83.94ms","84.94ms","85.94ms","86.94ms","87.94ms","88.94ms","89.94ms",    "90.94ms","91.94ms","92.94ms","93.94ms","94.94ms","95.94ms","96.94ms","97.94ms","98.94ms","99.94ms", },
      new string[] { "0.95ms","1.95ms","2.95ms","3.95ms","4.95ms","5.95ms","6.95ms","7.95ms","8.95ms","9.95ms",    "10.95ms","11.95ms","12.95ms","13.95ms","14.95ms","15.95ms","16.95ms","17.95ms","18.95ms","19.95ms",    "20.95ms","21.95ms","22.95ms","23.95ms","24.95ms","25.95ms","26.95ms","27.95ms","28.95ms","29.95ms",    "30.95ms","31.95ms","32.95ms","33.95ms","34.95ms","35.95ms","36.95ms","37.95ms","38.95ms","39.95ms",    "40.95ms","41.95ms","42.95ms","43.95ms","44.95ms","45.95ms","46.95ms","47.95ms","48.95ms","49.95ms",    "50.95ms","51.95ms","52.95ms","53.95ms","54.95ms","55.95ms","56.95ms","57.95ms","58.95ms","59.95ms",    "60.95ms","61.95ms","62.95ms","63.95ms","64.95ms","65.95ms","66.95ms","67.95ms","68.95ms","69.95ms",    "70.95ms","71.95ms","72.95ms","73.95ms","74.95ms","75.95ms","76.95ms","77.95ms","78.95ms","79.95ms",    "80.95ms","81.95ms","82.95ms","83.95ms","84.95ms","85.95ms","86.95ms","87.95ms","88.95ms","89.95ms",    "90.95ms","91.95ms","92.95ms","93.95ms","94.95ms","95.95ms","96.95ms","97.95ms","98.95ms","99.95ms", },
      new string[] { "0.96ms","1.96ms","2.96ms","3.96ms","4.96ms","5.96ms","6.96ms","7.96ms","8.96ms","9.96ms",    "10.96ms","11.96ms","12.96ms","13.96ms","14.96ms","15.96ms","16.96ms","17.96ms","18.96ms","19.96ms",    "20.96ms","21.96ms","22.96ms","23.96ms","24.96ms","25.96ms","26.96ms","27.96ms","28.96ms","29.96ms",    "30.96ms","31.96ms","32.96ms","33.96ms","34.96ms","35.96ms","36.96ms","37.96ms","38.96ms","39.96ms",    "40.96ms","41.96ms","42.96ms","43.96ms","44.96ms","45.96ms","46.96ms","47.96ms","48.96ms","49.96ms",    "50.96ms","51.96ms","52.96ms","53.96ms","54.96ms","55.96ms","56.96ms","57.96ms","58.96ms","59.96ms",    "60.96ms","61.96ms","62.96ms","63.96ms","64.96ms","65.96ms","66.96ms","67.96ms","68.96ms","69.96ms",    "70.96ms","71.96ms","72.96ms","73.96ms","74.96ms","75.96ms","76.96ms","77.96ms","78.96ms","79.96ms",    "80.96ms","81.96ms","82.96ms","83.96ms","84.96ms","85.96ms","86.96ms","87.96ms","88.96ms","89.96ms",    "90.96ms","91.96ms","92.96ms","93.96ms","94.96ms","95.96ms","96.96ms","97.96ms","98.96ms","99.96ms", },
      new string[] { "0.97ms","1.97ms","2.97ms","3.97ms","4.97ms","5.97ms","6.97ms","7.97ms","8.97ms","9.97ms",    "10.97ms","11.97ms","12.97ms","13.97ms","14.97ms","15.97ms","16.97ms","17.97ms","18.97ms","19.97ms",    "20.97ms","21.97ms","22.97ms","23.97ms","24.97ms","25.97ms","26.97ms","27.97ms","28.97ms","29.97ms",    "30.97ms","31.97ms","32.97ms","33.97ms","34.97ms","35.97ms","36.97ms","37.97ms","38.97ms","39.97ms",    "40.97ms","41.97ms","42.97ms","43.97ms","44.97ms","45.97ms","46.97ms","47.97ms","48.97ms","49.97ms",    "50.97ms","51.97ms","52.97ms","53.97ms","54.97ms","55.97ms","56.97ms","57.97ms","58.97ms","59.97ms",    "60.97ms","61.97ms","62.97ms","63.97ms","64.97ms","65.97ms","66.97ms","67.97ms","68.97ms","69.97ms",    "70.97ms","71.97ms","72.97ms","73.97ms","74.97ms","75.97ms","76.97ms","77.97ms","78.97ms","79.97ms",    "80.97ms","81.97ms","82.97ms","83.97ms","84.97ms","85.97ms","86.97ms","87.97ms","88.97ms","89.97ms",    "90.97ms","91.97ms","92.97ms","93.97ms","94.97ms","95.97ms","96.97ms","97.97ms","98.97ms","99.97ms", },
      new string[] { "0.98ms","1.98ms","2.98ms","3.98ms","4.98ms","5.98ms","6.98ms","7.98ms","8.98ms","9.98ms",    "10.98ms","11.98ms","12.98ms","13.98ms","14.98ms","15.98ms","16.98ms","17.98ms","18.98ms","19.98ms",    "20.98ms","21.98ms","22.98ms","23.98ms","24.98ms","25.98ms","26.98ms","27.98ms","28.98ms","29.98ms",    "30.98ms","31.98ms","32.98ms","33.98ms","34.98ms","35.98ms","36.98ms","37.98ms","38.98ms","39.98ms",    "40.98ms","41.98ms","42.98ms","43.98ms","44.98ms","45.98ms","46.98ms","47.98ms","48.98ms","49.98ms",    "50.98ms","51.98ms","52.98ms","53.98ms","54.98ms","55.98ms","56.98ms","57.98ms","58.98ms","59.98ms",    "60.98ms","61.98ms","62.98ms","63.98ms","64.98ms","65.98ms","66.98ms","67.98ms","68.98ms","69.98ms",    "70.98ms","71.98ms","72.98ms","73.98ms","74.98ms","75.98ms","76.98ms","77.98ms","78.98ms","79.98ms",    "80.98ms","81.98ms","82.98ms","83.98ms","84.98ms","85.98ms","86.98ms","87.98ms","88.98ms","89.98ms",    "90.98ms","91.98ms","92.98ms","93.98ms","94.98ms","95.98ms","96.98ms","97.98ms","98.98ms","99.98ms", },
      new string[] { "0.99ms","1.99ms","2.99ms","3.99ms","4.99ms","5.99ms","6.99ms","7.99ms","8.99ms","9.99ms",    "10.99ms","11.99ms","12.99ms","13.99ms","14.99ms","15.99ms","16.99ms","17.99ms","18.99ms","19.99ms",    "20.99ms","21.99ms","22.99ms","23.99ms","24.99ms","25.99ms","26.99ms","27.99ms","28.99ms","29.99ms",    "30.99ms","31.99ms","32.99ms","33.99ms","34.99ms","35.99ms","36.99ms","37.99ms","38.99ms","39.99ms",    "40.99ms","41.99ms","42.99ms","43.99ms","44.99ms","45.99ms","46.99ms","47.99ms","48.99ms","49.99ms",    "50.99ms","51.99ms","52.99ms","53.99ms","54.99ms","55.99ms","56.99ms","57.99ms","58.99ms","59.99ms",    "60.99ms","61.99ms","62.99ms","63.99ms","64.99ms","65.99ms","66.99ms","67.99ms","68.99ms","69.99ms",    "70.99ms","71.99ms","72.99ms","73.99ms","74.99ms","75.99ms","76.99ms","77.99ms","78.99ms","79.99ms",    "80.99ms","81.99ms","82.99ms","83.99ms","84.99ms","85.99ms","86.99ms","87.99ms","88.99ms","89.99ms",    "90.99ms","91.99ms","92.99ms","93.99ms","94.99ms","95.99ms","96.99ms","97.99ms","98.99ms","99.99ms", },
    };
  }
}

#endregion


#region Assets/Photon/Fusion/Runtime/Utilities/FusionScalableIMGUI.cs

namespace Fusion {
  using System.Reflection;
  using UnityEngine;

  /// <summary>
  /// In-Game IMGUI style used for the <see cref="FusionBootstrapDebugGUI"/> interface.
  /// </summary>
  public static class FusionScalableIMGUI {
    private static GUISkin _scalableSkin;

    private static void InitializedGUIStyles(GUISkin baseSkin) {
      _scalableSkin = baseSkin == null ? GUI.skin : baseSkin;

      // If no skin was provided, make the built in GuiSkin more tolerable.
      if (baseSkin == null) {
        _scalableSkin = GUI.skin;
        _scalableSkin.button.alignment = TextAnchor.MiddleCenter;
        _scalableSkin.label.alignment = TextAnchor.MiddleCenter;
        _scalableSkin.textField.alignment = TextAnchor.MiddleCenter;

        _scalableSkin.button.normal.background = _scalableSkin.box.normal.background;
        _scalableSkin.button.hover.background = _scalableSkin.window.normal.background;

        _scalableSkin.button.normal.textColor = new Color(.8f, .8f, .8f);
        _scalableSkin.button.hover.textColor = new Color(1f, 1f, 1f);
        _scalableSkin.button.active.textColor = new Color(1f, 1f, 1f);
        _scalableSkin.button.border = new RectOffset(6, 6, 6, 6);
        _scalableSkin.window.border = new RectOffset(8, 8, 8, 10);
      } else {
        // Use the supplied skin as the base.
        _scalableSkin = baseSkin;
      }
    }

    /// <summary>
    /// Get the custom scalable skin, already resized to the current screen. Provides the height, width, padding and margin used.
    /// </summary>
    /// <returns></returns>
    public static GUISkin GetScaledSkin(GUISkin baseSkin, out float height, out float width, out int padding, out int margin, out float boxLeft) {

      if (_scalableSkin == null) {
        InitializedGUIStyles(baseSkin);
      }

      var dimensions = ScaleGuiSkinToScreenHeight();
      height = dimensions.Item1;
      width = dimensions.Item2;
      padding = dimensions.Item3;
      margin = dimensions.Item4;
      boxLeft = dimensions.Item5;
      return _scalableSkin;
    }

    /// <summary>
    /// Modifies a skin to make it scale with screen height.
    /// </summary>
    /// <param name="skin"></param>
    /// <returns>Returns (height, width, padding, top-margin, left-box-margin) values applied to the GuiSkin</returns>
    public static (float, float, int, int, float) ScaleGuiSkinToScreenHeight() {

      bool isVerticalAspect = Screen.height > Screen.width;
      bool isSuperThin = Screen.height / Screen.width > (17f / 9f);

      float height = Screen.height * .08f;
      float width = System.Math.Min(Screen.width * .9f, Screen.height * .6f);
      int padding = (int)(height / 4);
      int margin = (int)(height / 8);
      float boxLeft = (Screen.width - width) * .5f;

      int fontsize = (int)(isSuperThin ? (width - (padding * 2)) * .07f : height * .4f);
      var margins = new RectOffset(0, 0, margin, margin);

      _scalableSkin.button.fontSize = fontsize;
      _scalableSkin.button.margin = margins;
      _scalableSkin.label.fontSize = fontsize;
      _scalableSkin.label.padding = new RectOffset(padding, padding, padding, padding);
      _scalableSkin.textField.fontSize = fontsize;
      _scalableSkin.window.padding = new RectOffset(padding, padding, padding, padding);
      _scalableSkin.window.margin = new RectOffset(margin, margin, margin, margin);

      return (height, width, padding, margin, boxLeft);
    }
  }
}

#endregion


#region Assets/Photon/Fusion/Runtime/Utilities/FusionUnitySceneManagerUtils.cs

﻿namespace Fusion {
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using System.Text;
  using System.Threading.Tasks;
  using UnityEditor;
  using UnityEngine;
  using UnityEngine.SceneManagement;

  public static class FusionUnitySceneManagerUtils {

    public class SceneEqualityComparer : IEqualityComparer<Scene> {
      public bool Equals(Scene x, Scene y) {
        return x.handle == y.handle;
      }

      public int GetHashCode(Scene obj) {
        return obj.handle;
      }
    }

    public static bool IsAddedToBuildSettings(this Scene scene) {
      if (scene.buildIndex < 0) {
        return false;
      }
      // yep that's a thing: https://docs.unity3d.com/ScriptReference/SceneManagement.Scene-buildIndex.html
      if (scene.buildIndex >= SceneManager.sceneCountInBuildSettings) {
        return false;
      }
      return true;
    }

#if UNITY_EDITOR
    public static bool AddToBuildSettings(Scene scene) {
      if (IsAddedToBuildSettings(scene)) {
        return false;
      }

      EditorBuildSettings.scenes =
        new[] { new EditorBuildSettingsScene(scene.path, true) }
        .Concat(EditorBuildSettings.scenes)
        .ToArray();

      Debug.Log($"Added '{scene.path}' as first entry in Build Settings.");
      return true;
    }
#endif

    public static LocalPhysicsMode GetLocalPhysicsMode(this Scene scene) {
      LocalPhysicsMode mode = LocalPhysicsMode.None;
      if (scene.GetPhysicsScene() != Physics.defaultPhysicsScene) {
        mode |= LocalPhysicsMode.Physics3D;
      }
      if (scene.GetPhysicsScene2D() != Physics2D.defaultPhysicsScene) {
        mode |= LocalPhysicsMode.Physics2D;
      }
      return mode;
    }

    /// <summary>
    /// Finds all components of type <typeparam name="T"/> in the scene.
    /// </summary>
    /// <param name="scene"></param>
    /// <param name="includeInactive"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public static T[] GetComponents<T>(this Scene scene, bool includeInactive) where T : Component {
      return GetComponents<T>(scene, includeInactive, out _);
    }
    
    /// <summary>
    /// Finds all components of type <typeparam name="T"/> in the scene.
    /// </summary>
    /// <param name="scene"></param>
    /// <param name="includeInactive"></param>
    /// <param name="rootObjects"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public static T[] GetComponents<T>(this Scene scene, bool includeInactive, out GameObject[] rootObjects) where T : Component {
      rootObjects = scene.GetRootGameObjects();
      
      var partialResult = new List<T>();
      var result        = new List<T>();

      foreach (var go in rootObjects) {
        // depth-first, according to docs and verified by our tests
        go.GetComponentsInChildren(includeInactive: includeInactive, partialResult);
        // AddRange accepts IEnumerable, so there would be an alloc
        foreach (var comp in partialResult) {
          result.Add(comp);
        }
      }
      return result.ToArray(); 
    }
    
    private static readonly List<GameObject> _reusableGameObjectList = new List<GameObject>();
    
    /// <summary>
    /// Finds all components of type <typeparam name="T"/> in the scene.
    /// </summary>
    /// <param name="scene"></param>
    /// <param name="results"></param>
    /// <param name="includeInactive"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public static void GetComponents<T>(this Scene scene, List<T> results, bool includeInactive) where T : Component {
      var rootObjects = _reusableGameObjectList;
      scene.GetRootGameObjects(rootObjects);
      results.Clear();
      
      var partialResult = new List<T>();

      foreach (var go in rootObjects) {
        // depth-first, according to docs and verified by our tests
        go.GetComponentsInChildren(includeInactive: includeInactive, partialResult);
        // AddRange accepts IEnumerable, so there would be an alloc
        foreach (var comp in partialResult) {
          results.Add(comp);
        }
      }
    }
    
    /// <summary>
    /// Finds the first instance of type <typeparam name="T"/> in the scene. Returns null if no instance found.
    /// </summary>
    /// <param name="scene"></param>
    /// <param name="includeInactive"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public static T FindComponent<T>(this Scene scene, bool includeInactive = false) where T : Component {
      var rootObjects = _reusableGameObjectList;
      scene.GetRootGameObjects(rootObjects);

      foreach (var go in rootObjects) {
        // depth-first, according to docs and verified by our tests
        var found = go.GetComponentInChildren<T>(includeInactive);
        if (found != null) {
          return found;
        }
      }
      return null;
    }

    public static bool CanBeUnloaded(this Scene scene) {
      if (!scene.isLoaded) {
        return false;
      }
      
      for (int i = 0; i < SceneManager.sceneCount; ++i) {
        var s = SceneManager.GetSceneAt(i);
        if (s != scene && s.isLoaded) {
          return true;
        }
      }
      return false;
    }

    public static string Dump(this Scene scene) {
      StringBuilder result = new StringBuilder();

      result.Append("[UnityScene:");
      
      if (scene.IsValid()) {
        result.Append(scene.name);
        result.Append(", isLoaded:").Append(scene.isLoaded);
        result.Append(", buildIndex:").Append(scene.buildIndex);
        result.Append(", isDirty:").Append(scene.isDirty);
        result.Append(", path:").Append(scene.path);
        result.Append(", rootCount:").Append(scene.rootCount);
        result.Append(", isSubScene:").Append(scene.isSubScene);
      } else {
        result.Append("<Invalid>");
      }

      result.Append(", handle:").Append(scene.handle);
      result.Append("]");
      return result.ToString();
    }

    public static string Dump(this LoadSceneParameters loadSceneParameters) {
      return $"[LoadSceneParameters: {loadSceneParameters.loadSceneMode}, localPhysicsMode:{loadSceneParameters.localPhysicsMode}]";
    }
    
    public static int GetSceneBuildIndex(string nameOrPath) {
      if (nameOrPath.IndexOf('/') >= 0) {
        return SceneUtility.GetBuildIndexByScenePath(nameOrPath);
      } else {
        for (int i = 0; i < SceneManager.sceneCountInBuildSettings; ++i) {
          var scenePath = SceneUtility.GetScenePathByBuildIndex(i);
          GetFileNameWithoutExtensionPosition(scenePath, out var nameIndex, out var nameLength);
          if (nameLength == nameOrPath.Length && string.Compare(scenePath, nameIndex, nameOrPath, 0, nameLength, true) == 0) {
            return i;
          }
        }

        return -1;
      }
    }
    
    public static int GetSceneIndex(IList<string> scenePathsOrNames, string nameOrPath) {
      if (nameOrPath.IndexOf('/') >= 0) {
        return scenePathsOrNames.IndexOf(nameOrPath);
      } else {
        for (int i = 0; i < scenePathsOrNames.Count; ++i) {
          var scenePath = scenePathsOrNames[i];
          GetFileNameWithoutExtensionPosition(scenePath, out var nameIndex, out var nameLength);
          if (nameLength == nameOrPath.Length && string.Compare(scenePath, nameIndex, nameOrPath, 0, nameLength, true) == 0) {
            return i;
          }
        }
        return -1;
      }
    }

    public static void GetFileNameWithoutExtensionPosition(string nameOrPath, out int index, out int length) {
      var lastSlash = nameOrPath.LastIndexOf('/');
      if (lastSlash >= 0) {
        index = lastSlash + 1;
      } else {
        index = 0;
      }

      var lastDot = nameOrPath.LastIndexOf('.');
      if (lastDot > index) {
        length = lastDot - index;
      } else {
        length = nameOrPath.Length - index;
      }
    }
  }
}


#endregion


#region Assets/Photon/Fusion/Runtime/Utilities/RunnerVisibility/NetworkRunnerVisibilityExtensions.cs

namespace Fusion
{
  using System.Collections.Generic;
  using System.Linq;
  using UnityEngine;
  using Analyzer;

    public static class NetworkRunnerVisibilityExtensions {
   
      // TODO: Still needed?
      [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
      private static void ResetAllSimulationStatics() {
        ResetStatics();
      }
      
      /// <summary>
      /// Types that fusion.runtime isn't aware of, which need to be found using names instead.
      /// </summary>
      [StaticField(StaticFieldResetMode.None)]
      private static readonly string[] RecognizedBehaviourNames = 
      {
        "EventSystem"
      };
      
      [StaticField(StaticFieldResetMode.None)]
      private static readonly System.Type[] RecognizedBehaviourTypes = {
        typeof(IRunnerVisibilityRecognizedType),
        typeof(Renderer),
        typeof(AudioListener),
        typeof(Camera),
        typeof(Canvas),
        typeof(Light)
      };

      
      private static readonly Dictionary<NetworkRunner, RunnerVisibility> DictionaryLookup;

      // Constructor
      static NetworkRunnerVisibilityExtensions() {
        DictionaryLookup = new Dictionary<NetworkRunner, RunnerVisibility>();
      }

      private class RunnerVisibility {
        public bool IsVisible { get; set; } = true;

        public LinkedList<RunnerVisibilityLink> Nodes = new LinkedList<RunnerVisibilityLink>();
      }

      private static bool _commonLinksWithMissingInputAuthNeedRefresh;

      public static void RetryRefreshCommonLinks() {
        _commonLinksWithMissingInputAuthNeedRefresh = false;
        RefreshCommonObjectVisibilities();
      }

      public static void EnableVisibilityExtension(this NetworkRunner runner) {
        if (runner && DictionaryLookup.ContainsKey(runner) == false) {
          DictionaryLookup.Add(runner, new RunnerVisibility());
        }
      }

      public static void DisableVisibilityExtension(this NetworkRunner runner) {
        if (runner && DictionaryLookup.ContainsKey(runner)) {
          DictionaryLookup.Remove(runner);
        }
      }
      
      public static bool HasVisibilityEnabled(this NetworkRunner runner) {
        return DictionaryLookup.ContainsKey(runner);
      }
      
      public static bool GetVisible(this NetworkRunner runner) {
        if (runner == null) {
          return false;
        }
        
        if (DictionaryLookup.TryGetValue(runner, out var runnerVisibility) == false) {
          return true;
        }

        return runnerVisibility.IsVisible;
      }

      public static void SetVisible(this NetworkRunner runner, bool isVisibile) {
        runner.GetVisibilityInfo().IsVisible = isVisibile;
        RefreshRunnerVisibility(runner);
      }

      private static LinkedList<RunnerVisibilityLink> GetVisibilityNodes(this NetworkRunner runner) {
        if (runner == false) {
          return null;
        }
        return runner.GetVisibilityInfo()?.Nodes;
      }

      private static RunnerVisibility GetVisibilityInfo(this NetworkRunner runner) {
        if (DictionaryLookup.TryGetValue(runner, out var runnerVisibility) == false) {
          return null;
        }

        return runnerVisibility;
      }
      
      /// <summary>
      /// Find all component types that contribute to a scene rendering, and associate them with a <see cref="RunnerVisibilityLink"/> component, 
      /// and add them to the runner's list of visibility nodes.
      /// </summary>
      /// <param name="go"></param>
      /// <param name="runner"></param>
      public static void AddVisibilityNodes(this NetworkRunner runner, GameObject go) {
        runner.EnableVisibilityExtension();

        // Check for flag component which indicates object has already been cataloged.
        if (go.GetComponent<RunnerVisibilityLinksRoot>()) {return;}
      
        go.AddComponent<RunnerVisibilityLinksRoot>();

        // Have user EnableOnSingleRunner add RunnerVisibilityControl before we process all nodes.
        var existingEnableOnSingles = go.transform.GetComponentsInChildren<EnableOnSingleRunner>(true);
        List<RunnerVisibilityLink> existingNodes = go.GetComponentsInChildren<RunnerVisibilityLink>(false).ToList();
      
        foreach (var enableOnSingleRunner in existingEnableOnSingles) {
          enableOnSingleRunner.AddNodes(existingNodes);
        }

        CollectBehavioursAndAddNodes(go, runner, existingNodes);

        RefreshRunnerVisibility(runner);
      }

      private static void CollectBehavioursAndAddNodes(GameObject go, NetworkRunner runner, List<RunnerVisibilityLink> existingNodes) {

        // If any changes are made to the commons, we need a full refresh.
        var commonsNeedRefresh = false;

        var components = go.transform.GetComponentsInChildren<Component>(true);
        foreach (var comp in components) {
          var nodeAlreadyExists = false;

          // Check for broken/missing components
          if (comp == null) continue;
          // See if devs added a node for this behaviour already
          foreach (var existingNode in existingNodes)
            if (existingNode.Component == comp) {
              nodeAlreadyExists = true;
              if (existingNode.IsOnSingleRunner) {
                AddNodeToCommonLookup(existingNode);
                RegisterNode(existingNode, runner, comp);
                commonsNeedRefresh = true;
              }
              break;
            }

          if (nodeAlreadyExists)
            continue;

          // No existing node was found, create one if this comp is a recognized render type

          var type = comp.GetType();
          // Only add if comp is one of the behaviours considered render related.
          if (IsRecognizedByRunnerVisibility(type)) {
            var node = comp.gameObject.AddComponent<RunnerVisibilityLink>();
            RegisterNode(node, runner, comp);
          }
        }

        if (commonsNeedRefresh) {
          _commonLinksWithMissingInputAuthNeedRefresh = true;
          RefreshCommonObjectVisibilities();
        }
      }

      internal static bool IsRecognizedByRunnerVisibility(this System.Type type) {
        // First try the faster type based lookup
        foreach (var recognizedType in RecognizedBehaviourTypes) {
          if (recognizedType.IsAssignableFrom(type))
            return true;
        }

        // The try the slower string based (for namespace references not included in the Fusion core).
        var typename = type.Name;
        foreach (var recognizedNames in RecognizedBehaviourNames) {
          if (typename.Contains(recognizedNames))
            return true;
        }

        return false;
      }
      
      private static void RegisterNode(RunnerVisibilityLink link, NetworkRunner runner, Component comp) {
// #if DEBUG
//         if (runner.GetVisibilityNodes().Contains(node))
//           Log.Warn($"{nameof(RunnerVisibilityNode)} on '{node.name}' already has been registered.");
// #endif

        runner.GetVisibilityNodes().AddLast(link);
        link.Initialize(comp, runner);
      }

      public static void UnregisterNode(this RunnerVisibilityLink link) {

        if (link == null || link._runner == null) {
          return;
        }

        var runner                  = link._runner;
        var runnerIsNullOrDestroyed = !(runner);

        if (!runnerIsNullOrDestroyed) {
          var visNodes = link._runner.GetVisibilityNodes();
          if (visNodes == null) {
            // No VisibilityNodes collection, likely a shutdown condition.
            return;
          } 
        }

        if (runnerIsNullOrDestroyed == false && runner.GetVisibilityNodes().Contains(link)) {
          runner.GetVisibilityNodes().Remove(link);
        }

        // // Remove from the Runner list.
        // if (!ReferenceEquals(node, null) && node._node != null && node._node.List != null) {
        //   node._node.List.Remove(node);
        // }

        if (link.Guid != null) {

          if (CommonObjectLookup.TryGetValue(link.Guid, out var clones)) {
            if (clones.Contains(link)) {
              clones.Remove(link);
            }

            // if this is the last instance of this _guid... remove the entry from the lookup.
            if (clones.Count == 0) {
              CommonObjectLookup.Remove(link.Guid);
            }
          }
        }
      }


      private static void AddNodeToCommonLookup(RunnerVisibilityLink link) {
        var guid = link.Guid;
        if (string.IsNullOrEmpty(guid))
          return;

        if (!CommonObjectLookup.TryGetValue(guid, out var clones)) {
          clones = new List<RunnerVisibilityLink>();
          CommonObjectLookup.Add(guid, clones);
        }
        clones.Add(link);
      }
      
      /// <summary>
      /// Reapplies a runner's IsVisibile setting to all of its registered visibility nodes.
      /// </summary>
      /// <param name="runner"></param>
      /// <param name="refreshCommonObjects"></param>
      private static void RefreshRunnerVisibility(NetworkRunner runner, bool refreshCommonObjects = true) {

        // Trying to refresh before the runner has setup.
        if (runner.GetVisibilityNodes() == null) {
          //Log.Warn($"{nameof(NetworkRunner)} visibility can't be changed. Not ready yet.");
          return;
        }

        bool enable = runner.GetVisible();

        foreach (var node in runner.GetVisibilityNodes()) {

          // This should never be null, but just in case...
          if (node == null) {
            continue;
          }
          node.SetEnabled(enable);
        }
        if (refreshCommonObjects) {
          RefreshCommonObjectVisibilities();
        }
      }
      
      
      /// <summary>
      /// Dictionary lookup for manually added visibility nodes (which indicates only one instance should be visible at a time), 
      /// which returns a list of nodes for a given LocalIdentifierInFile.
      /// </summary>
      [StaticField]
      private readonly static Dictionary<string, List<RunnerVisibilityLink>> CommonObjectLookup = new Dictionary<string, List<RunnerVisibilityLink>>();
      
      internal static void RefreshCommonObjectVisibilities() {
        var runners = NetworkRunner.GetInstancesEnumerator();
        NetworkRunner serverRunner = null;
        NetworkRunner clientRunner = null;
        NetworkRunner firstRunner = null;
        bool foundInputAuth = false;

        // First find the runner for each preference.
        while (runners.MoveNext()) {
          var runner = runners.Current;
          // Exclude inactive runners TODO: may not be needed after this list is patched to contain only active
          if (!runner.IsRunning || !runner.GetVisible() || runner.IsShutdown)
            continue;

          if (runner.IsServer) {
            serverRunner = runner;
          }
          
          if (!clientRunner && runner.GameMode != GameMode.Server) {
            clientRunner = runner;
          }

          if (!firstRunner) {
            firstRunner = runner;
          }
        }

        // loop all common objects, making sure to activate only one peer instance.
        foreach (var kvp in CommonObjectLookup) {
          var clones = kvp.Value;
          if (clones.Count > 0) {
            NetworkRunner prefRunner;
            var firstClone = clones[0];

            switch (firstClone.PreferredRunner) {
              case RunnerVisibilityLink.PreferredRunners.Server:
                prefRunner = serverRunner;
                break;
              case RunnerVisibilityLink.PreferredRunners.Client:
                prefRunner = clientRunner;
                break;
              case RunnerVisibilityLink.PreferredRunners.Auto:
                prefRunner = firstRunner;
                break;
              default:
                prefRunner = null;
                break;
            }

            foundInputAuth = false;
            foreach (var clone in clones) {
              if (clone.PreferredRunner == RunnerVisibilityLink.PreferredRunners.InputAuthority) {
                var inputFound = clone.IsInputAuth();
                clone.Enabled = inputFound && clone._runner.GetVisible();
                foundInputAuth |= inputFound;
              } else {
                clone.Enabled = ReferenceEquals(clone._runner, prefRunner);
              }
            }

            if (firstClone.PreferredRunner == RunnerVisibilityLink.PreferredRunners.InputAuthority) {
              if (foundInputAuth == false && _commonLinksWithMissingInputAuthNeedRefresh) {
                // Signal to refresh later when the object has input information.
                _commonLinksWithMissingInputAuthNeedRefresh = false;
                firstClone.InvokeRefreshCommonObjectVisibilities(1f);
              }
            }
          }
        }
      }

      [StaticFieldResetMethod]
      internal static void ResetStatics() {
        CommonObjectLookup.Clear();
      }
    }
}


#endregion

#endif

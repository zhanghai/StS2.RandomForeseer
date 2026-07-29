using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models;

namespace RandomForeseer.RandomForeseerCode.Common.Mirrors;

/// <summary>
/// Describes the exact-runtime-type policy selected for one mirrored virtual method.
/// </summary>
internal enum MirrorDispatchKind
{
    /// <summary>The runtime type inherits the base implementation and needs no mirror handler.</summary>
    NotOverridden,

    /// <summary>The runtime type has a registered prediction handler.</summary>
    Handled,

    /// <summary>The runtime type has an unregistered override with a best-effort inferred prediction handler.</summary>
    Inferred,

    /// <summary>The override was reviewed and intentionally has no prediction-relevant behavior.</summary>
    Ignored,

    /// <summary>The override is gameplay-relevant but has no safe prediction handler.</summary>
    Unsupported
}

internal readonly record struct MirrorDispatchResult(MirrorDispatchKind Kind);

internal readonly record struct MirrorDispatchResult<TResult>(MirrorDispatchKind Kind, TResult Value);

/// <summary>
/// Analyzes one unregistered virtual action override and, when structurally supported, creates its inferred handler.
/// </summary>
/// <remarks>
/// Inference is performed once per exact runtime type and must depend only on type-level method structure. The returned
/// handler may resolve instance-dependent values or skip inapplicable candidates, but it must not capture a receiver or
/// context instance. The registry owns incomplete-risk recording for inferred invocations.
/// </remarks>
internal delegate Action<TBase, TContext>? ModelMethodMirrorInferer<TBase, TContext>(Type runtimeType, MethodInfo overrideMethod)
    where TBase : class
    where TContext : IPredictionMirrorContext<TBase>;

/// <summary>
/// Dispatches one mirrored virtual action method against the exact runtime type of one receiver.
/// </summary>
/// <remarks>
/// Invocation-wide behavior such as listener enumeration and hook ordering belongs to the adapter layered over this
/// registry. All registrations must finish before the first query or invocation because lookup results are cached.
/// </remarks>
internal sealed class ModelMethodMirrorRegistry<TBase, TContext>(MirrorMethodSpec method)
    where TBase : class
    where TContext : IPredictionMirrorContext<TBase>
{
    // All registrations must be completed before the first invocation. Registries are built during
    // static initialization and do not support runtime registration.
    private readonly Dictionary<Type, LookupResult> _registrations = [];
    private readonly Dictionary<Type, LookupResult> _lookupCache = [];

    private ModelMethodMirrorInferer<TBase, TContext>? _inferer;
    private bool _allowInference = true;

    /// <summary>
    /// Gets or sets whether unregistered gameplay overrides may use the registered Type-level inferer.
    /// </summary>
    /// <remarks>
    /// Changing this policy clears only resolved Type lookups. Explicit handled and ignored registrations remain
    /// intact, and the registered inferer remains available if inference is enabled again.
    /// </remarks>
    public bool AllowInference
    {
        get => _allowInference;
        set
        {
            if (_allowInference != value)
            {
                _allowInference = value;
                _lookupCache.Clear();
            }
        }
    }

    public void Register<TModel>(Action<TModel, TContext> handler)
        where TModel : TBase
    {
        var type = typeof(TModel);
        ValidateOverride(type);
        // Exact type matching is intentional: derived models must be reviewed independently.
        _registrations.Add(type, new(
            MirrorDispatchKind.Handled,
            (receiver, context) => handler((TModel)receiver, context)));
    }

    public void RegisterIgnored<TModel>()
        where TModel : TBase
    {
        var type = typeof(TModel);
        ValidateOverride(type);
        _registrations.Add(type, new(MirrorDispatchKind.Ignored, null));
    }

    /// <summary>
    /// Registers the single type-level fallback used to infer unregistered, gameplay-relevant overrides.
    /// </summary>
    public void RegisterInferer(ModelMethodMirrorInferer<TBase, TContext> inferer)
    {
        ArgumentNullException.ThrowIfNull(inferer);

        _inferer = _inferer is not null
            ? throw new InvalidOperationException($"Mirror for {method.Name} already has a registered inferer.")
            : inferer;
    }

    /// <summary>
    /// Returns whether the exact runtime type has an explicitly registered handler without resolving or caching a
    /// fallback lookup.
    /// </summary>
    public bool HasRegisteredHandler(TBase receiver)
    {
        return _registrations.TryGetValue(receiver.GetType(), out var registration) &&
            registration.Kind is MirrorDispatchKind.Handled;
    }

    public MirrorDispatchResult Invoke(TBase receiver, TContext context)
    {
        var (kind, handler) = Lookup(receiver.GetType());
        if (kind is MirrorDispatchKind.NotOverridden or MirrorDispatchKind.Ignored)
        {
            return new(kind);
        }

        using (context.PushDispatchSource(receiver, method))
        {
            switch (kind)
            {
                case MirrorDispatchKind.Handled:
                    handler!(receiver, context);
                    break;

                case MirrorDispatchKind.Inferred:
                    // Inferred mirrors are always incomplete by definition. Record the risk before invoking the handler.
                    context.RecordMethodMirrorIncompleteRisk();
                    handler!(receiver, context);
                    break;

                case MirrorDispatchKind.Unsupported:
                    context.RecordMethodNotMirroredRisk();
                    break;

                default:
                    throw new InvalidOperationException($"Unexpected mirror dispatch kind {kind}.");
            }

            return new(kind);
        }
    }

    private LookupResult Lookup(Type type)
    {
        if (_registrations.TryGetValue(type, out var result) ||
            _lookupCache.TryGetValue(type, out result))
        {
            return result;
        }

        if (!method.TryGetOverride(type, out var overrideMethod))
        {
            result = new(MirrorDispatchKind.NotOverridden, null);
        }
        else if (TryGetMod(overrideMethod, out var mod) && mod.manifest?.affectsGameplay is false)
        {
            Entry.Logger.Info(
                $"Mirror for {method.Name} ignored unsupported {type.FullName} from non-gameplay mod {mod.manifest?.id}.");
            result = new(MirrorDispatchKind.Ignored, null);
        }
        else if (_allowInference && _inferer?.Invoke(type, overrideMethod) is { } inferredHandler)
        {
            Entry.Logger.Info(
                $"Mirror for {method.Name} will best-effort infer behavior for unregistered {type.FullName}.");
            result = new(MirrorDispatchKind.Inferred, inferredHandler);
        }
        else
        {
            Entry.Logger.Warn(
                $"Mirror for {method.Name} does not safely handle {type.FullName}; preview may omit that modifier.");
            result = new(MirrorDispatchKind.Unsupported, null);
        }

        _lookupCache.Add(type, result);
        return result;
    }

    private void ValidateOverride(Type type)
    {
        if (!method.TryGetOverride(type, out _))
        {
            throw new InvalidOperationException(
                $"{type.FullName} does not override {method.BaseMethod.DeclaringType?.FullName}.{method.Name}.");
        }
    }

    private static bool TryGetMod(MethodInfo overrideMethod, [NotNullWhen(true)] out Mod? mod)
    {
        var declaringType = overrideMethod.DeclaringType;
        if (declaringType is null)
        {
            mod = null;
            return false;
        }

        // StS2 v0.109.0 centralizes base-game, mod, and test mock type lookup here.
        mod = AssemblyInfo.ModForType(declaringType, out var isBaseGame);
        return !isBaseGame && mod is not null;
    }

    private readonly record struct LookupResult(
        MirrorDispatchKind Kind,
        Action<TBase, TContext>? Handler);
}

/// <summary>
/// Dispatches one mirrored virtual result method and falls back to a caller-provided value when no result is available.
/// </summary>
/// <remarks>
/// This result-producing counterpart is used by methods such as <see cref="OrbModel.Evoke"/>. Registrations must finish
/// before the first invocation because exact-type lookup results are cached.
/// </remarks>
internal sealed class ModelMethodMirrorRegistry<TBase, TContext, TResult>(MirrorMethodSpec method)
    where TBase : class
    where TContext : IPredictionMirrorContext<TBase>
{
    // All registrations must be completed before the first invocation. Registries are built during
    // static initialization and do not support runtime registration.
    private readonly Dictionary<Type, LookupResult> _lookups = [];

    public void Register<TModel>(Func<TModel, TContext, TResult> handler)
        where TModel : TBase
    {
        var type = typeof(TModel);
        ValidateOverride(type);
        _lookups.Add(type, new(
            MirrorDispatchKind.Handled,
            (receiver, context) => handler((TModel)receiver, context)));
    }

    public MirrorDispatchResult<TResult> Invoke(
        TBase receiver,
        TContext context,
        TResult defaultResult)
    {
        var (kind, handler) = Lookup(receiver.GetType());
        if (kind is MirrorDispatchKind.NotOverridden or MirrorDispatchKind.Ignored)
        {
            return new(kind, defaultResult);
        }

        using (context.PushDispatchSource(receiver, method))
        {
            if (kind is MirrorDispatchKind.Handled)
            {
                return new(kind, handler!(receiver, context));
            }

            context.RecordMethodNotMirroredRisk();
            return new(kind, defaultResult);
        }
    }

    private LookupResult Lookup(Type type)
    {
        if (_lookups.TryGetValue(type, out var result))
        {
            return result;
        }

        if (!method.TryGetOverride(type, out var overrideMethod))
        {
            result = new(MirrorDispatchKind.NotOverridden, null);
        }
        else
        {
            Entry.Logger.Warn(
                $"Mirror for {method.Name} does not safely handle {type.FullName}; preview may omit that behavior.");
            result = new(MirrorDispatchKind.Unsupported, null);
        }

        _lookups.Add(type, result);
        return result;
    }

    private void ValidateOverride(Type type)
    {
        if (!method.TryGetOverride(type, out _))
        {
            throw new InvalidOperationException(
                $"{type.FullName} does not override {method.BaseMethod.DeclaringType?.FullName}.{method.Name}.");
        }
    }

    private readonly record struct LookupResult(
        MirrorDispatchKind Kind,
        Func<TBase, TContext, TResult>? Handler);
}

using System;
using System.Threading.Tasks;
using Transpose;
using Transpose.Core;

namespace Tesserae.Lauschwerk
{
    /// <summary>
    /// The single place an <see cref="IPromise"/> from lauschwerk becomes a <see cref="Task"/>.
    ///
    /// Centralised because the conversion has one sharp edge and one piece of ceremony, and neither
    /// is worth rediscovering at forty call sites:
    ///
    /// <para>
    /// <b>Never <c>await</c> an <see cref="IPromise"/> directly.</b> Its awaiter is typed as handing
    /// back the resolved values as <c>object[]</c>, but <c>Transpose.toPromise</c> passes a native
    /// promise straight through - so the awaited value is the single resolved value, reading
    /// <c>.Length</c> on it gives <c>undefined</c>, and the result silently vanishes. No error, no
    /// warning; a document that loaded fine looks like it never resolved.
    /// </para>
    ///
    /// <para>
    /// <c>Task.FromPromise</c>'s selector parameter is typed as <see cref="Delegate"/>, so a bare
    /// lambda does not compile against it ("Cannot convert lambda expression to type 'Delegate'").
    /// The <c>new Func&lt;object, T&gt;(...)</c> below is that cast, written once.
    /// </para>
    ///
    /// A rejected promise faults the task, which is what lets a lauschwerk failure surface as a C#
    /// exception a component can turn into a <see cref="PdfError"/>.
    /// </summary>
    internal static class PromiseHelper
    {
        /// <summary>
        /// Awaits <paramref name="promise"/> and hands its resolved value back as
        /// <typeparamref name="T"/>.
        ///
        /// <b>Do not pass an array of an <c>[External]</c> type as <typeparamref name="T"/>.</b> The
        /// compiler materialises an array type argument by calling <c>System.Array.type(element)</c>,
        /// which reads <c>$$fullname</c> off the element type - and an <c>[External]</c> declaration
        /// has no runtime metadata, so it throws
        /// <c>Cannot read properties of undefined (reading '$$fullname')</c> before the promise is
        /// even awaited. A bare external interface is fine (nothing materialises it, and the cast is
        /// erased); an array of one is not. Await as <c>object</c> and cast the result instead - see
        /// the declarations in Interop/.
        /// </summary>
        internal static Task<T> ToTask<T>(IPromise promise)
        {
            return Task.FromPromise<T>(promise, new Func<object, T>(resolved => (T)resolved));
        }

        /// <summary>
        /// Awaits <paramref name="promise"/> and projects its resolved value through
        /// <paramref name="select"/> - for the calls whose resolved value is a raw lauschwerk object this
        /// package wraps in something friendlier.
        /// </summary>
        internal static Task<T> ToTask<T>(IPromise promise, Func<object, T> select)
        {
            return Task.FromPromise<T>(promise, select);
        }

        /// <summary>Awaits <paramref name="promise"/> for its completion, discarding the resolved value.</summary>
        internal static Task ToTask(IPromise promise)
        {
            return Task.FromPromise<bool>(promise, new Func<object, bool>(_ => true));
        }

        /// <summary>
        /// Adapts a <see cref="Task"/> into the native JavaScript <c>Promise</c> that a lauschwerk
        /// callback interface expects - the localization bridge's <c>translate</c> and <c>get</c>, and
        /// anything else lauschwerk awaits.
        ///
        /// <c>Transpose.toPromise</c> is the runtime's own adapter - the same one the compiler emits
        /// for <c>await</c> - so a faulted task rejects the promise instead of the exception being
        /// swallowed. <c>Task&lt;T&gt;</c> derives from <see cref="Task"/>, so this one overload
        /// covers both.
        /// </summary>
        internal static IPromise AsPromise(Task task) => PromiseExtensions.ToPromise(task);
    }
}

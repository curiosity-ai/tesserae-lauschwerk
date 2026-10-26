using System;
using System.Collections.Generic;
using static Transpose.Core.dom;

namespace Tesserae.Alkove
{
    /// <summary>
    /// The teardown something has accumulated - event subscriptions, animation loops, audio nodes -
    /// released together when it is torn down.
    ///
    /// alkove's <c>on(type, fn)</c> hands back the unsubscribe function itself, so what is held here is
    /// a release closure rather than a handle. That happens to be the shape a BCL list can hold - a
    /// <c>List&lt;TSomeExternalInterface&gt;</c> cannot be constructed at all, because an
    /// <c>[External]</c> declaration has no runtime type metadata for the generic to name, and fails
    /// with "Cannot read properties of undefined (reading '$$name')".
    ///
    /// Disposal is defensive: one release that throws must not strand the rest.
    /// </summary>
    public sealed class DisposableBag
    {
        private readonly List<Action> _releases = new List<Action>();

        /// <summary>Takes ownership of a teardown action. Null actions are ignored.</summary>
        public void Add(Action release)
        {
            if (release is null) return;

            _releases.Add(release);
        }

        /// <summary>How many teardown actions are held.</summary>
        public int Count => _releases.Count;

        /// <summary>Runs everything held, then empties the bag.</summary>
        public void DisposeAll()
        {
            foreach (var release in _releases)
            {
                try
                {
                    release();
                }
                catch (Exception exception)
                {
                    console.error("Tesserae.Alkove: a teardown action threw on release", exception);
                }
            }

            _releases.Clear();
        }
    }
}

// Boot-time helpers. Loaded from App.razor, before any component can invoke them.
//
// The splash this manages lives in App.razor as STATIC markup, outside the Blazor component tree — it exists to
// cover the window between the first byte and the SignalR circuit connecting, during which a prerender:false app
// has literally nothing rendered. Because Blazor does not own that element, it cannot remove it; AuthenticationGate
// calls hideSplash once the app has its own content on screen.
window.projectHubBoot = {
  hideSplash: function () {
    const splash = document.getElementById("ph-splash");

    if (!splash) {
      // Already removed. Calling twice is harmless — a circuit that reconnects re-runs the gate's first render.
      return;
    }

    // Fade rather than cut, so handing over to the app reads as a transition instead of a flicker. The element is
    // removed on transitionend (with a timeout fallback, since transitionend does not fire if the element is
    // display:none, in a background tab, or if the user has reduced-motion preferences that skip the transition).
    splash.style.transition = "opacity 180ms ease";
    splash.style.opacity = "0";
    splash.style.pointerEvents = "none";

    let removed = false;
    const remove = function () {
      if (removed) {
        return;
      }
      removed = true;
      splash.remove();
    };

    splash.addEventListener("transitionend", remove, { once: true });
    window.setTimeout(remove, 400);
  },
};

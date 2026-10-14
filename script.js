/* =========================================================
   Checkmate — site interactions
   - mobile nav
   - X-sheet demo (built from a data model, click/drag to scrub)
   - timing chips (1s / 2s / 3s) that actually re-space the exposure
   - copy-to-clipboard for install commands
   - tiny scroll-reveal for section heads
   ========================================================= */

(() => {
  "use strict";

  /* ---------- Mobile nav ---------- */
  const toggle = document.querySelector(".menu-toggle");
  const links  = document.querySelector(".nav-links");
  if (toggle && links) {
    toggle.addEventListener("click", () => {
      const open = links.classList.toggle("open");
      toggle.setAttribute("aria-expanded", String(open));
    });
    links.addEventListener("click", (e) => {
      if (e.target.tagName === "A") {
        links.classList.remove("open");
        toggle.setAttribute("aria-expanded", "false");
      }
    });
  }

  /* ---------- X-sheet demo ---------- */
  const FRAMES = 24;
  const LAYERS = ["A", "B", "C"];

  // Each layer is a list of drawing-start frames. Timing determines spacing.
  // Layer A = character (heavy exposure, "on twos" by default)
  // Layer B = effects (sparser)
  // Layer C = background (mostly held, one drawing)
  let model = {
    A: makeStarts(1, FRAMES, 2),                 // on 2s
    B: [1, 5, 9, 13, 17, 21],                    // sparse keys
    C: [1]                                       // single held background
  };
  let activeLayer = "A";
  let activeFrame = 1;

  const table = document.getElementById("xshet-table");

  function makeStarts(start, end, step) {
    const out = [];
    for (let f = start; f <= end; f += step) out.push(f);
    return out;
  }

  /** Convert a layer's start frames into per-frame cell states. */
  function cellState(starts, frame) {
    if (starts.includes(frame)) return "start";
    // is this frame covered by a held drawing?
    let covered = false;
    let isEnd = false;
    for (let i = 0; i < starts.length; i++) {
      const s = starts[i];
      const next = starts[i + 1] ?? (FRAMES + 1);
      if (frame > s && frame < next) {
        covered = true;
        if (frame === next - 1) isEnd = true;
        break;
      }
    }
    if (covered) return isEnd ? "end" : "held";
    return "empty";
  }

  function cellGlyph(state) {
    return { start: "n", held: "│", end: "✓", empty: "" }[state] || "";
  }

  function render() {
    if (!table) return;

    // header
    let html = "<thead><tr><th>Frame</th>";
    LAYERS.forEach(l => { html += `<th>${l}</th>`; });
    html += "</tr></thead><tbody>";

    for (let f = 1; f <= FRAMES; f++) {
      const active = f === activeFrame ? " class=\"active\"" : "";
      html += `<tr${active}><td>${f}</td>`;
      for (const layer of LAYERS) {
        const st = cellState(model[layer], f);
        const glyph = cellGlyph(st);
        const classes = ["cell"];
        if (st === "start") classes.push("start");
        if (st === "held")  classes.push("held");
        if (st === "end")   classes.push("end");
        if (layer === activeLayer) classes.push("on-active");
        html += `<td class="${classes.join(" ")}" data-frame="${f}" data-layer="${layer}">${glyph}</td>`;
      }
      html += "</tr>";
    }
    html += "</tbody>";
    table.innerHTML = html;
  }

  /* --- Click & drag to scrub --- */
  function setFrameFromEvent(e) {
    const td = e.target.closest("td.cell");
    if (!td) return;
    activeFrame = parseInt(td.dataset.frame, 10);
    activeLayer = td.dataset.layer;
    render();
  }

  if (table) {
    let dragging = false;

    table.addEventListener("mousedown", (e) => {
      dragging = true;
      setFrameFromEvent(e);
      e.preventDefault();
    });
    table.addEventListener("mousemove", (e) => {
      if (!dragging) return;
      // Only update if we're over a new row
      const td = e.target.closest("td.cell");
      if (td && parseInt(td.dataset.frame, 10) !== activeFrame) {
        activeFrame = parseInt(td.dataset.frame, 10);
        render();
      }
    });
    window.addEventListener("mouseup", () => { dragging = false; });
  }

  /* --- Timing chips (1s / 2s / 3s) --- */
  document.querySelectorAll(".chip[data-timing]").forEach(chip => {
    chip.addEventListener("click", () => {
      document.querySelectorAll(".chip[data-timing]").forEach(c => c.classList.remove("active"));
      chip.classList.add("active");

      const step = parseInt(chip.dataset.timing, 10);
      // Retime the active layer's keys. Layer C (background) stays held.
      if (activeLayer !== "C") {
        model[activeLayer] = makeStarts(1, FRAMES, step);
      }
      render();
    });
  });

  /* ---------- Copy button ---------- */
  document.querySelectorAll(".copy-btn").forEach(btn => {
    btn.addEventListener("click", async () => {
      const text = btn.dataset.copy || "";
      try {
        await navigator.clipboard.writeText(text);
        btn.textContent = "Copied";
        btn.classList.add("copied");
        setTimeout(() => {
          btn.textContent = "Copy";
          btn.classList.remove("copied");
        }, 1600);
      } catch {
        btn.textContent = "Select & copy";
      }
    });
  });

  /* ---------- Tiny scroll reveal for section heads ---------- */
  const revealTargets = document.querySelectorAll(
    ".section-head, .compare-card, .feature, .contrib, .steps li, .decision-list li"
  );
  if ("IntersectionObserver" in window && revealTargets.length) {
    revealTargets.forEach(el => {
      el.style.opacity = "0";
      el.style.transform = "translateY(12px)";
      el.style.transition = "opacity .5s ease, transform .5s ease";
    });

    const io = new IntersectionObserver((entries) => {
      entries.forEach(entry => {
        if (entry.isIntersecting) {
          entry.target.style.opacity = "1";
          entry.target.style.transform = "translateY(0)";
          io.unobserve(entry.target);
        }
      });
    }, { rootMargin: "0px 0px -8% 0px", threshold: 0.05 });

    revealTargets.forEach(el => io.observe(el));
  }

  /* ---------- Initial render ---------- */
  render();
})();

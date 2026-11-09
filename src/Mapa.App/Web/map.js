(function () {
  'use strict';

  var map = L.map('map', { zoomControl: true }).setView([-14.2, -51.9], 4);

  // Leaflet's own "Leaflet" link/flag in the attribution control is just library branding
  // (not required); drop it to keep only the OpenStreetMap credit their tile usage policy requires.
  map.attributionControl.setPrefix(false);

  L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
    maxZoom: 18,
    attribution: '&copy; OpenStreetMap contributors',
  }).addTo(map);

  var clusteringEnabled = new URLSearchParams(location.search).get('clustering') !== 'false';

  var markerLayer = clusteringEnabled
    ? L.markerClusterGroup({ spiderfyOnMaxZoom: true, showCoverageOnHover: false, iconCreateFunction: createClusterIcon })
    : L.featureGroup();
  markerLayer.addTo(map);

  var propagationLayer = L.layerGroup().addTo(map);

  // Propagation playback speed (multiplier of the 2.4 s base cycle). Discrete steps so the
  // slider lands on round values; slow motion is the common case for reading the sequence.
  var speedSteps = [0.1, 0.25, 0.5, 0.75, 1, 1.5, 2];
  var defaultSpeedIndex = 4;
  var speedIndex = defaultSpeedIndex;
  try {
    var savedIndex = parseInt(localStorage.getItem('mapa.propagationSpeedIndex'), 10);
    if (savedIndex >= 0 && savedIndex < speedSteps.length) speedIndex = savedIndex;
  } catch (e) { /* storage unavailable: keep default */ }
  var lastRingMarkers = [];

  function formatSpeed(v) {
    return String(v).replace('.', ',') + '×';
  }

  var SpeedControl = L.Control.extend({
    options: { position: 'bottomleft' },
    onAdd: function () {
      var div = L.DomUtil.create('div', 'propagation-speed-control');
      div.innerHTML =
        '<label for="propagation-speed">Velocidade da propagação</label>' +
        '<div class="propagation-speed-row">' +
        '<input id="propagation-speed" type="range" min="0" max="' + (speedSteps.length - 1) + '" step="1" value="' + speedIndex + '" />' +
        '<span class="propagation-speed-value">' + formatSpeed(speedSteps[speedIndex]) + '</span>' +
        '</div>';
      L.DomEvent.disableClickPropagation(div);
      L.DomEvent.disableScrollPropagation(div);
      var input = div.querySelector('input');
      var valueLabel = div.querySelector('.propagation-speed-value');
      input.addEventListener('input', function () {
        speedIndex = parseInt(input.value, 10);
        valueLabel.textContent = formatSpeed(speedSteps[speedIndex]);
        try { localStorage.setItem('mapa.propagationSpeedIndex', String(speedIndex)); } catch (e) { /* ignore */ }
        propagationLayer.clearLayers();
        addPulseRings(lastRingMarkers);
      });
      // Double-click resets to 1×.
      input.addEventListener('dblclick', function () {
        input.value = String(defaultSpeedIndex);
        input.dispatchEvent(new Event('input'));
      });
      return div;
    },
  });
  var speedControl = new SpeedControl();
  var speedControlShown = false;

  function setSpeedControlVisible(visible) {
    if (visible && !speedControlShown) { speedControl.addTo(map); speedControlShown = true; }
    else if (!visible && speedControlShown) { speedControl.remove(); speedControlShown = false; }
  }

  // Worst-case aggregation: the cluster wears the color of its most severe child. Averaging the
  // child colors (the previous behavior) blended sags and swells from opposite ends of the
  // diverging scale into an indistinct mid-tone, so every cluster looked the same shade of green
  // and a serious event stayed invisible until the user zoomed in. severityRank comes from C#
  // (higher = worse) because "worst" means different things per mode.
  //
  // Size carries no meaning: it is just the smallest circle the count still fits inside, so a
  // cluster stays as close as possible to the 9px individual markers.
  function clusterSizePx(count) {
    var digits = String(count).length;
    return digits <= 2 ? 22 : digits === 3 ? 26 : 30;
  }

  function createClusterIcon(cluster) {
    var children = cluster.getAllChildMarkers();
    var size = clusterSizePx(children.length);
    return L.divIcon({
      html: '<div class="mapa-cluster-icon" style="width:' + size + 'px;height:' + size +
        'px;background:' + worstChildColor(children) + ';">' + children.length + '</div>',
      className: '',
      iconSize: L.point(size, size),
    });
  }

  function worstChildColor(children) {
    var worstColor = '#2ECC71';
    var worstRank = -Infinity;
    for (var i = 0; i < children.length; i++) {
      var rank = children[i].options.severityRank;
      if (typeof rank === 'number' && rank > worstRank) {
        worstRank = rank;
        worstColor = children[i].options.fillColor || worstColor;
      }
    }
    return worstColor;
  }

  function escapeHtml(value) {
    return String(value).replace(/[&<>"']/g, function (ch) {
      return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[ch];
    });
  }

  // Decodes an 8-bit quality string (e.g. "00000001") into human-readable flags.
  // Bit layout (LSB = bit 0, rightmost char):
  //   0: Início incerto  1: Fim incerto  2: Frames inválidos
  //   3: Sem frames recebidos  4: Timeout (≥1 min)  5-7: Reservados
  function decodeQuality(q) {
    if (!q || q.length !== 8) return null;
    var bit = function (n) { return q[7 - n] === '1'; };
    var flags = [];
    if (bit(0)) flags.push('bit 0 — Início incerto');
    if (bit(1)) flags.push('bit 1 — Fim incerto');
    if (bit(2)) flags.push('bit 2 — Há dados inválidos durante o evento');
    if (bit(3)) flags.push('bit 3 — Há frames perdidos durante o evento');
    if (bit(4)) flags.push('bit 4 — Evento encerrado por timeout');
    return flags.length === 0 ? 'OK' : flags.join(' · ');
  }

  function buildPopupHtml(marker) {
    var rows = marker.events.map(function (ev) {
      var decoded = decodeQuality(ev.quality);
      var qualityCell = decoded !== null
        ? '<td title="' + escapeHtml(decoded) + '" style="cursor:help;text-decoration:underline dotted">' + escapeHtml(ev.quality) + '</td>'
        : '<td>' + escapeHtml(ev.quality) + '</td>';
      return '<tr>' +
        '<td>' + escapeHtml(ev.eventType) + '</td>' +
        '<td>' + escapeHtml(ev.date) + '</td>' +
        '<td>' + escapeHtml(ev.startTime) + '</td>' +
        '<td>' + escapeHtml(ev.endTime) + '</td>' +
        '<td>' + escapeHtml(ev.extremeValue) + '</td>' +
        qualityCell +
        '</tr>';
    }).join('');

    var detectionRow = '';
    if (marker.detectionRank && marker.detectionRank > 0) {
      var delayStr = marker.detectionRank === 1
        ? '1ª detecção'
        : marker.detectionRank + 'ª detecção  (+' + marker.detectionDelayMs.toFixed(0) + ' ms)';
      detectionRow = '<p class="pmu-detection-rank">' + escapeHtml(delayStr) + '</p>';
    }

    return '' +
      '<div class="pmu-popup">' +
      '<h4>' + escapeHtml(marker.label || marker.idName) + '</h4>' +
      detectionRow +
      '<table>' +
      '<thead><tr><th>Tipo</th><th>Data</th><th>Inicio</th><th>Fim</th><th>Valor</th><th title="Passe o mouse sobre o valor para ver o significado dos bits">Quality ⓘ</th></tr></thead>' +
      '<tbody>' + rows + '</tbody>' +
      '</table>' +
      '</div>';
  }

  // Heat layer: cold tones where the disturbance was mild, hot where it was severe. Weight comes
  // from C# as an already-normalized 0..1 intensity, so the gradient means the same thing as the
  // legend regardless of the metric's raw units.
  var heatLayer = null;
  var lastMarkers = [];
  var heatmapEnabled = false;
  var heatPending = false;

  map.on('resize', function () {
    if (heatmapEnabled && heatPending) applyRenderMode();
  });

  function renderHeatmap(markers) {
    if (heatLayer) {
      map.removeLayer(heatLayer);
      heatLayer = null;
    }

    var points = markers.map(function (m) {
      var intensity = typeof m.intensity === 'number' ? m.intensity : 0.5;
      // Floor keeps a mild-but-real detection visible instead of fading to nothing.
      return [m.lat, m.lon, Math.max(0.15, Math.min(1, intensity))];
    });

    if (points.length === 0) return;

    // Leaflet.heat draws straight onto a canvas sized from the map container, and throws
    // (getImageData with width 0) if the map is not laid out yet -- which happens whenever the
    // panel is hidden. Defer to the next resize instead of crashing the page.
    var size = map.getSize();
    if (!size || size.x === 0 || size.y === 0) {
      heatPending = true;
      return;
    }
    heatPending = false;

    heatLayer = L.heatLayer(points, {
      // Kept tight on purpose. Leaflet.heat *sums* overlapping weights, and at national zoom
      // substations 180 km apart sit only ~18 px apart, so a generous radius makes neighbouring
      // blobs add up and saturate: a mild sag next to a severe one both come out red. A small
      // radius keeps each site's colour honest; zooming in separates them further.
      radius: 18,
      blur: 14,
      // maxZoom drives Leaflet.heat's zoom attenuation (1/2^(maxZoom-zoom)). Any value above the
      // current zoom crushes every weight toward zero at national zoom; 0 disables it so the
      // weights we send are the weights that get drawn.
      maxZoom: 0,
      // minOpacity is a floor applied to EVERY point. Set high (0.35) it flattened severe and mild
      // alike to the same alpha, leaving density as the only visible signal -- three mild PMUs
      // outshone the actual epicenter. Keep it just above invisible.
      minOpacity: 0.05,
      max: 1.0,
      gradient: { 0.0: '#2166AC', 0.35: '#3BAFDA', 0.55: '#2ECC71', 0.75: '#F1C40F', 1.0: '#E74C3C' },
    });
    heatLayer.addTo(map);
  }

  function applyRenderMode() {
    if (heatmapEnabled) {
      markerLayer.clearLayers();
      propagationLayer.clearLayers();
      setSpeedControlVisible(false);
      renderHeatmap(lastMarkers);
    } else {
      if (heatLayer) {
        map.removeLayer(heatLayer);
        heatLayer = null;
      }
      renderMarkers(lastMarkers);
    }
  }

  function setMarkers(markers) {
    lastMarkers = markers;
    applyRenderMode();
  }

  function setHeatmapEnabled(enabled) {
    heatmapEnabled = !!enabled;
    applyRenderMode();
  }

  function renderMarkers(markers) {
    markerLayer.clearLayers();
    propagationLayer.clearLayers();

    var circles = markers.map(function (marker) {
      var circle;
      if (marker.detectionRank === 1) {
        var sizePx = marker.radiusPx * 2;
        circle = L.marker([marker.lat, marker.lon], {
          icon: L.divIcon({
            className: '',
            html: '<div class="first-detection-marker" style="width:' + sizePx + 'px;height:' + sizePx + 'px;background:' + marker.colorHex + ';">★</div>',
            iconSize: [sizePx, sizePx],
            iconAnchor: [marker.radiusPx, marker.radiusPx],
          }),
          severityRank: marker.severityRank,
          fillColor: marker.colorHex,
        });
      } else {
        circle = L.circleMarker([marker.lat, marker.lon], {
          radius: marker.radiusPx,
          fillColor: marker.colorHex,
          color: '#333333',
          weight: 1,
          opacity: 0.9,
          fillOpacity: 0.85,
          severityRank: marker.severityRank,
        });
      }

      circle.bindPopup(buildPopupHtml(marker), { maxWidth: 700, minWidth: 200 });


      circle.on('click', function () {
        if (window.chrome && window.chrome.webview) {
          window.chrome.webview.postMessage({ type: 'markerClicked', idName: marker.idName });
        }
      });

      return circle;
    });

    if (clusteringEnabled) {
      markerLayer.addLayers(circles);
    } else {
      circles.forEach(function (c) { markerLayer.addLayer(c); });
      addPulseRings(markers);
    }
    if (clusteringEnabled) setSpeedControlVisible(false);
  }

  function addPulseRings(markers) {
    lastRingMarkers = markers;
    var ranked = markers
      .filter(function (m) { return m.detectionRank > 0; })
      .sort(function (a, b) { return a.detectionRank - b.detectionRank; });

    var N = ranked.length;
    setSpeedControlVisible(N > 0);
    if (N === 0) return;

    // Timing budget (must match the CSS @keyframes percentages above):
    //   cycleSec         = total animation loop duration (2.4 s)
    //   initialPauseSec  = silence before the 1st PMU fires
    //   pulseDurationSec = how long one ring takes to expand + fade  (≈ 15% of cycle)
    //   resetPauseSec    = silence after the last PMU before the wave restarts
    //
    // Each ring fires with a delay proportional to the real detectionDelayMs so the visual
    // timing mirrors the actual propagation sequence.  The real span is stretched/compressed
    // to fill the available window inside one cycle.
    var cycleSec         = 2.4;
    var initialPauseSec  = 0.2;
    var pulseDurationSec = 0.36;  // 15 % of 2.4 s
    var resetPauseSec    = 1.24;
    var availableSec     = cycleSec - initialPauseSec - pulseDurationSec - resetPauseSec; // 0.6 s

    var maxDelayMs = ranked[ranked.length - 1].detectionDelayMs || 0;
    // scale: real-ms → animation-seconds; if all PMUs detected simultaneously use even spread
    var scaleFactor = maxDelayMs > 0 ? availableSec / maxDelayMs * 1000 : (N > 1 ? availableSec / (N - 1) : 0);

    // Speed slider: stretch the whole cycle (duration and every delay) by 1/speed, so the
    // keyframe percentages in map.css keep the same proportions.
    var timeScale = 1 / speedSteps[speedIndex];

    ranked.forEach(function (marker) {
      // Positive delay: ring stays invisible (via animation-fill-mode:backwards) until its slot.
      var delay  = (initialPauseSec + (marker.detectionDelayMs || 0) * scaleFactor / 1000) * timeScale;
      var sizePx = marker.radiusPx * 2;

      var ringIcon = L.divIcon({
        className: '',
        html: '<div class="detection-ring" style="' +
          'width:'  + sizePx + 'px;' +
          'height:' + sizePx + 'px;' +
          'color:'  + marker.colorHex + ';' +
          'animation-duration:' + (cycleSec * timeScale).toFixed(3) + 's;' +
          'animation-delay:' + delay.toFixed(3) + 's' +
          '"></div>',
        iconSize: [sizePx, sizePx],
        iconAnchor: [marker.radiusPx, marker.radiusPx],
      });

      L.marker([marker.lat, marker.lon], {
        icon: ringIcon,
        interactive: false,
      }).addTo(propagationLayer);
    });
  }

  function fitBounds() {
    var bounds;
    if (heatmapEnabled || !clusteringEnabled) {
      bounds = lastMarkers.length
        ? L.latLngBounds(lastMarkers.map(function (m) { return [m.lat, m.lon]; }))
        : null;
    } else {
      bounds = markerLayer.getBounds();
    }

    if (bounds && bounds.isValid()) {
      // In event-detail mode (no clustering) zoom in further so markers separate clearly.
      var opts = clusteringEnabled
        ? { padding: [30, 30], maxZoom: 10 }
        : { padding: [60, 60] };
      map.fitBounds(bounds, opts);
    } else {
      map.setView([-14.2, -51.9], 4);
    }
  }

  function handleMessage(raw) {
    var msg = typeof raw === 'string' ? JSON.parse(raw) : raw;
    switch (msg.type) {
      case 'setMarkers':
        setMarkers(msg.payload.markers || []);
        break;
      case 'setHeatmap':
        setHeatmapEnabled(msg.payload.enabled);
        break;
      case 'fitBounds':
        fitBounds();
        break;
      default:
        break;
    }
  }

  if (window.chrome && window.chrome.webview) {
    window.chrome.webview.addEventListener('message', function (event) { handleMessage(event.data); });
  }

  // Exposed for manual testing in a plain browser / DevTools console (no WebView2 host present).
  window.__mapaTestHook = {
    setMarkers: setMarkers,
    fitBounds: fitBounds,
    setHeatmapEnabled: setHeatmapEnabled,
    hasHeatLayer: function () { return heatLayer !== null; },
    clusterCount: function () { return markerLayer.getLayers().length; },
    refreshSize: function () { map.invalidateSize(false); return map.getSize(); },
    latLngToPixel: function (lat, lon) { return map.latLngToContainerPoint([lat, lon]); },
  };
})();

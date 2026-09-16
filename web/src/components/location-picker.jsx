import { useEffect, useRef, useState } from 'react';
import Box from '@mui/material/Box';
import TextField from '@mui/material/TextField';
import Button from '@mui/material/Button';
import Typography from '@mui/material/Typography';
import CircularProgress from '@mui/material/CircularProgress';
import { MapContainer, TileLayer, Marker, useMapEvents } from 'react-leaflet';
import L from 'leaflet';
import 'leaflet/dist/leaflet.css';
import markerIcon2x from 'leaflet/dist/images/marker-icon-2x.png?url';
import markerIcon from 'leaflet/dist/images/marker-icon.png?url';
import markerShadow from 'leaflet/dist/images/marker-shadow.png?url';
import axios, { endpoints } from 'src/lib/axios';
import { parseLatLng, linkFromLatLng, esEnlaceCorto } from 'src/lib/google-maps-url';

// El icono por omision de Leaflet apunta a rutas relativas que un bundler
// como Vite no resuelve solo -- sin esto el marcador se ve como un cuadrado
// roto. Se reemplaza una sola vez, para todo mapa que use este archivo.
delete L.Icon.Default.prototype._getIconUrl;
L.Icon.Default.mergeOptions({ iconRetinaUrl: markerIcon2x, iconUrl: markerIcon, shadowUrl: markerShadow });

// Cochabamba, como centro por omision cuando todavia no hay ninguna
// ubicacion elegida.
const CENTRO_POR_DEFECTO = { lat: -17.3895, lng: -66.1568 };

function ManejadorDeClicks({ onPick }) {
  useMapEvents({
    click(e) {
      onPick(e.latlng.lat, e.latlng.lng);
    },
  });
  return null;
}

/**
 * Elegir la ubicacion de una sede en un mapa, o pegar un enlace de Google
 * Maps directamente -- las dos formas terminan en el mismo campo de texto,
 * asi que da lo mismo por cual se empiece: elegir en el mapa completa el
 * enlace solo, y pegar un enlace con coordenadas (uno ya generado por este
 * mismo selector, por ejemplo) mueve el marcador a donde corresponde.
 *
 * El mapa en si es OpenStreetMap via Leaflet, no la API de Google Maps: esta
 * ultima pide una clave con facturacion habilitada que el proyecto no
 * tiene, mientras que Leaflet no necesita ninguna. El enlace que se genera
 * al elegir un punto sigue siendo un enlace real de Google Maps
 * (google.com/maps?q=lat,lng), que es lo que importa para quien lo recibe.
 *
 * Un enlace pegado a mano no siempre trae sus coordenadas en el texto: los
 * "lugares" y los armados por el propio selector si las traen, pero un
 * enlace corto (el que da "Compartir" desde el celular, maps.app.goo.gl/...)
 * las esconde detras de un redireccionamiento que el navegador no puede
 * seguir entre origenes distintos -- de ahi que este componente le pida al
 * servidor que lo siga (ver ManageVenues.ResolveMapsLinkAsync) en vez de
 * intentarlo el mismo. Cuando ni eso alcanza, se avisa en vez de mostrar el
 * centro por omision como si fuera el lugar real.
 */
export function LocationPicker({ value, onChange }) {
  const parsedFromValue = parseLatLng(value);
  const [position, setPosition] = useState(parsedFromValue || CENTRO_POR_DEFECTO);
  const [posicionConocida, setPosicionConocida] = useState(!!parsedFromValue || !value);
  const [resolviendoEnlace, setResolviendoEnlace] = useState(false);
  const [query, setQuery] = useState('');
  const [buscando, setBuscando] = useState(false);
  const [errorBusqueda, setErrorBusqueda] = useState('');
  const mapRef = useRef(null);

  // Un enlace pegado a mano (o cargado al editar una sede existente) puede
  // traer sus propias coordenadas directamente, o esconderlas detras de un
  // enlace corto que hay que pedirle al servidor que resuelva primero.
  useEffect(() => {
    let cancelado = false;

    async function sincronizar() {
      const directo = parseLatLng(value);
      if (directo) {
        setPosition(directo);
        setPosicionConocida(true);
        mapRef.current?.setView([directo.lat, directo.lng], 15);
        return;
      }

      if (!value) {
        // Sede nueva, sin enlace todavia: no es un enlace que fallo, es que
        // todavia no se eligio nada.
        setPosicionConocida(true);
        return;
      }

      if (!esEnlaceCorto(value)) {
        setPosicionConocida(false);
        return;
      }

      setResolviendoEnlace(true);
      setPosicionConocida(false);
      try {
        const res = await axios.get(endpoints.resolveVenueMapsLink(value));
        if (cancelado) return;
        const resuelto = parseLatLng(res.data.resolvedUrl);
        if (resuelto) {
          setPosition(resuelto);
          setPosicionConocida(true);
          mapRef.current?.setView([resuelto.lat, resuelto.lng], 15);
        }
      } catch {
        // Se deja posicionConocida en false -- el aviso de abajo lo explica.
      } finally {
        if (!cancelado) setResolviendoEnlace(false);
      }
    }

    sincronizar();
    return () => { cancelado = true; };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [value]);

  const elegir = (lat, lng) => {
    setPosition({ lat, lng });
    setPosicionConocida(true);
    onChange(linkFromLatLng(lat, lng));
  };

  const buscar = async () => {
    if (!query.trim()) return;
    setBuscando(true);
    setErrorBusqueda('');
    try {
      const res = await fetch(
        'https://nominatim.openstreetmap.org/search?format=json&limit=1&q=' + encodeURIComponent(query),
      );
      const resultados = await res.json();
      if (resultados.length === 0) {
        setErrorBusqueda('No se encontró esa dirección.');
        return;
      }
      const lat = parseFloat(resultados[0].lat);
      const lng = parseFloat(resultados[0].lon);
      elegir(lat, lng);
      mapRef.current?.setView([lat, lng], 15);
    } catch {
      setErrorBusqueda('No se pudo buscar la dirección. Pegá el enlace directamente si ya lo tenés.');
    } finally {
      setBuscando(false);
    }
  };

  return (
    <Box>
      <Box sx={{ display: 'flex', gap: 1, mb: 1 }}>
        <TextField
          size="small"
          fullWidth
          label="Buscar dirección"
          value={query}
          onChange={(e) => setQuery(e.target.value)}
          onKeyDown={(e) => { if (e.key === 'Enter') { e.preventDefault(); buscar(); } }}
          placeholder="Ej: Av. Arce, La Paz"
        />
        <Button variant="outlined" onClick={buscar} disabled={buscando} sx={{ flexShrink: 0 }}>
          {buscando ? <CircularProgress size={18} /> : 'Buscar'}
        </Button>
      </Box>
      {errorBusqueda && (
        <Typography variant="caption" color="error" sx={{ display: 'block', mb: 1 }}>{errorBusqueda}</Typography>
      )}

      <Box sx={{ height: 240, borderRadius: 1, overflow: 'hidden', mb: 1 }}>
        <MapContainer
          ref={mapRef}
          center={[position.lat, position.lng]}
          zoom={parsedFromValue ? 15 : 12}
          style={{ height: '100%', width: '100%' }}
        >
          <TileLayer
            attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>'
            url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
          />
          <ManejadorDeClicks onPick={elegir} />
          {posicionConocida && (
            <Marker
              position={[position.lat, position.lng]}
              draggable
              eventHandlers={{ dragend: (e) => { const p = e.target.getLatLng(); elegir(p.lat, p.lng); } }}
            />
          )}
        </MapContainer>
      </Box>

      {resolviendoEnlace && (
        <Typography variant="caption" color="text.secondary" sx={{ display: 'flex', alignItems: 'center', gap: 0.5, mb: 1 }}>
          <CircularProgress size={12} /> Resolviendo el enlace…
        </Typography>
      )}
      {!resolviendoEnlace && !posicionConocida && (
        <Typography variant="caption" color="warning.main" sx={{ display: 'block', mb: 1 }}>
          No pudimos ubicar el punto exacto de ese enlace en el mapa. Buscá la dirección arriba o hacé
          click en el mapa para fijarlo — el enlace pegado se conserva igual.
        </Typography>
      )}
      {!resolviendoEnlace && posicionConocida && (
        <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mb: 1 }}>
          Hacé click en el mapa o arrastrá el marcador para ajustar el punto exacto.
        </Typography>
      )}

      <TextField
        label="Enlace de Google Maps"
        value={value}
        onChange={(e) => onChange(e.target.value)}
        fullWidth
        size="small"
        placeholder="https://maps.app.goo.gl/..."
        helperText="Se completa solo al elegir un punto arriba, o pegá aquí un enlace que ya tengas."
      />
    </Box>
  );
}

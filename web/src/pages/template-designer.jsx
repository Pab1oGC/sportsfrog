import { useState, useRef, useEffect } from 'react';
import { useParams, useNavigate } from 'react-router';
import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';
import Button from '@mui/material/Button';
import TextField from '@mui/material/TextField';
import MenuItem from '@mui/material/MenuItem';
import Alert from '@mui/material/Alert';
import IconButton from '@mui/material/IconButton';
import Tooltip from '@mui/material/Tooltip';
import Divider from '@mui/material/Divider';
import Paper from '@mui/material/Paper';
import Tabs from '@mui/material/Tabs';
import Tab from '@mui/material/Tab';
import Switch from '@mui/material/Switch';
import FormControlLabel from '@mui/material/FormControlLabel';
import CircularProgress from '@mui/material/CircularProgress';
import { Iconify } from 'src/components/iconify';
import { useApi, apiPut } from 'src/hooks/use-api';
import { endpoints, default as axios } from 'src/lib/axios';

/* ---------------------------------------------------------------------------
   Los limites son los de LayoutPolicy.cs, repetidos aca a proposito: sujetar
   el arrastre dentro de lo que la API acepta es la unica forma de que mover
   una caja con el mouse no termine en un 400 al guardar. "La imagen se sale
   de la cara" es literalmente el error que un editor de arrastre provoca.
   --------------------------------------------------------------------------- */

var MINIMO = 0.005;         // LayoutPolicy.MinimumExtent
var TAMANO_MAXIMO = 0.5;    // LayoutPolicy.MaximumTextSize
var MAXIMO_CAMPOS = 60;     // LayoutPolicy.MaximumFields

/** Las unicas propiedades que TemplateField conoce. Cualquier otra es un 400. */
var PROPIEDADES = ['source', 'x', 'y', 'w', 'h', 'size', 'font', 'align', 'fit', 'minSize', 'color', 'bold', 'text'];

/**
 * Las asas, por que borde mueve cada una.
 *
 * Ocho en lugar de cuatro esquinas porque ajustar solo el ancho de un texto
 * sin tocarle el alto es la operacion mas comun de todas, y con esquinas hay
 * que hacerla en dos pasos.
 */
var ASAS = [
  { id: 'nw', izq: true,  arr: true,  der: false, aba: false, cx: 0,   cy: 0,   cursor: 'nwse-resize' },
  { id: 'n',  izq: false, arr: true,  der: false, aba: false, cx: 0.5, cy: 0,   cursor: 'ns-resize' },
  { id: 'ne', izq: false, arr: true,  der: true,  aba: false, cx: 1,   cy: 0,   cursor: 'nesw-resize' },
  { id: 'e',  izq: false, arr: false, der: true,  aba: false, cx: 1,   cy: 0.5, cursor: 'ew-resize' },
  { id: 'se', izq: false, arr: false, der: true,  aba: true,  cx: 1,   cy: 1,   cursor: 'nwse-resize' },
  { id: 's',  izq: false, arr: false, der: false, aba: true,  cx: 0.5, cy: 1,   cursor: 'ns-resize' },
  { id: 'sw', izq: true,  arr: false, der: false, aba: true,  cx: 0,   cy: 1,   cursor: 'nesw-resize' },
  { id: 'w',  izq: true,  arr: false, der: false, aba: false, cx: 0,   cy: 0.5, cursor: 'ew-resize' }
];

function limitar(v, min, max) {
  return Math.min(max, Math.max(min, v));
}

function caraVacia(proporcion) {
  return { backgroundKey: null, aspectRatio: proporcion || 1.5875, fields: [] };
}

/** Solo lo que TemplateFace declara, con los campos podados igual. */
function limpiarCara(cara) {
  if (!cara) return null;
  return {
    backgroundKey: cara.backgroundKey || null,
    aspectRatio: cara.aspectRatio || 1.5875,
    fields: (cara.fields || []).map(limpiarCampo)
  };
}

function limpiarCampo(campo) {
  var limpio = {};
  PROPIEDADES.forEach(function(p) {
    if (campo[p] !== undefined) limpio[p] = campo[p];
  });
  return limpio;
}

export default function TemplateDesignerPage() {
  var params = useParams();
  var navigate = useNavigate();
  var templateId = params.id;

  var { data: plantilla, isLoading: cargando } = useApi(templateId ? endpoints.template(templateId) : null);

  // El catalogo se pide por tipo: la API ya filtra las fuentes que este
  // documento puede imprimir, asi que un certificado no llega a ofrecer la
  // fotografia del deportista, que rechazaria al guardar.
  var { data: catalogo } = useApi(
    plantilla ? endpoints.documentsDesign + '?kind=' + plantilla.kind : null
  );

  var [nombre, setNombre] = useState('');
  var [tamano, setTamano] = useState('');
  var [porDefecto, setPorDefecto] = useState(false);
  var [caras, setCaras] = useState({ front: caraVacia(), back: null });
  var [fondos, setFondos] = useState({ front: '', back: '' });
  var [cara, setCara] = useState('front');
  var [sel, setSel] = useState(null);
  var [error, setError] = useState('');
  var [aviso, setAviso] = useState('');
  var [guardando, setGuardando] = useState(false);
  var [subiendo, setSubiendo] = useState(false);
  var [cargado, setCargado] = useState(false);

  var lienzoRef = useRef(null);
  var gesto = useRef(null);
  var [medida, setMedida] = useState({ w: 0, h: 0 });

  // El codigo anterior hacia esto con una variable local que se reiniciaba en
  // cada render, asi que llamaba a setState durante el render y volvia a
  // pisar lo editado en cuanto SWR revalidaba.
  useEffect(function() {
    if (!plantilla || cargado) return;
    var l = plantilla.layout || {};
    setNombre(plantilla.name || '');
    setTamano(plantilla.pageSize || 'credential');
    setPorDefecto(!!plantilla.isDefault);
    setCaras({
      front: l.front ? limpiarCara(l.front) : caraVacia(),
      back: l.back ? limpiarCara(l.back) : null
    });
    setFondos({
      front: (plantilla.backgrounds && plantilla.backgrounds.front) || '',
      back: (plantilla.backgrounds && plantilla.backgrounds.back) || ''
    });
    setCargado(true);
  }, [plantilla, cargado]);

  // El tamano en pixeles del lienzo, porque el alto de un texto se expresa
  // como fraccion de la cara y para dibujarlo hace falta saber cuanto mide
  // esa cara en pantalla.
  useEffect(function() {
    var el = lienzoRef.current;
    if (!el || typeof ResizeObserver === 'undefined') return undefined;
    var observador = new ResizeObserver(function(entradas) {
      var r = entradas[0].contentRect;
      setMedida({ w: r.width, h: r.height });
    });
    observador.observe(el);
    return function() { observador.disconnect(); };
  }, [cargado, cara]);

  var caraActual = caras[cara] || caraVacia();
  var campos = caraActual.fields || [];
  var fuentes = (catalogo && catalogo.sources) || [];
  var tipografias = (catalogo && catalogo.fonts) || [];
  var tamanos = (catalogo && catalogo.pageSizes) || [];

  function fuenteDe(codigo) {
    return fuentes.find(function(f) { return f.code === codigo; });
  }

  function esImagen(campo) {
    var f = fuenteDe(campo.source);
    return !!f && f.shape === 'image';
  }

  function etiquetaDe(campo) {
    if (campo.source === 'text') return campo.text || 'Texto fijo';
    var f = fuenteDe(campo.source);
    return (f && f.label) || campo.source;
  }

  /* ------------------------------------------------------------------ estado */

  function cambiarCara(clave, cambios) {
    setCaras(function(previas) {
      var actual = previas[clave];
      if (!actual) return previas;
      var siguientes = Object.assign({}, previas);
      siguientes[clave] = Object.assign({}, actual, cambios);
      return siguientes;
    });
  }

  function cambiarCampo(idx, cambios) {
    setCaras(function(previas) {
      var actual = previas[cara];
      if (!actual || !actual.fields[idx]) return previas;
      var lista = actual.fields.slice();
      lista[idx] = Object.assign({}, lista[idx], cambios);
      var siguientes = Object.assign({}, previas);
      siguientes[cara] = Object.assign({}, actual, { fields: lista });
      return siguientes;
    });
  }

  function quitarCampo(idx) {
    setCaras(function(previas) {
      var actual = previas[cara];
      if (!actual) return previas;
      var siguientes = Object.assign({}, previas);
      siguientes[cara] = Object.assign({}, actual, {
        fields: actual.fields.filter(function(_, i) { return i !== idx; })
      });
      return siguientes;
    });
    setSel(null);
  }

  function agregarCampo(codigo) {
    var f = fuenteDe(codigo);
    if (!f) return;
    if (campos.length >= MAXIMO_CAMPOS) {
      setError('Una cara admite hasta ' + MAXIMO_CAMPOS + ' campos.');
      return;
    }

    var nuevo = f.shape === 'image'
      ? { source: codigo, x: 0.08, y: 0.25, w: 0.22, h: 0.35 }
      : {
          source: codigo, x: 0.08, y: 0.25, w: 0.5, size: 0.07,
          font: (catalogo.defaults && catalogo.defaults.font) || 'sans',
          align: (catalogo.defaults && catalogo.defaults.align) || 'left',
          fit: (catalogo.defaults && catalogo.defaults.fit) || 'shrink'
        };

    if (codigo === 'text') nuevo.text = 'Texto';

    setCaras(function(previas) {
      var actual = previas[cara];
      var siguientes = Object.assign({}, previas);
      siguientes[cara] = Object.assign({}, actual, { fields: [].concat(actual.fields, [nuevo]) });
      return siguientes;
    });
    setSel(campos.length);
  }

  /* ------------------------------------------------- geometria y arrastre */

  // Un campo de texto tambien es una caja: su alto es `size`, que es como lo
  // trata el generador. Tratar los dos por igual deja un solo modelo de
  // arrastre en lugar de dos que se parecen.
  function cajaDe(campo, imagen) {
    return {
      x: campo.x || 0,
      y: campo.y || 0,
      w: campo.w != null ? campo.w : (1 - (campo.x || 0)),
      h: imagen ? (campo.h || 0.1) : (campo.size || 0.06)
    };
  }

  function guardarCaja(idx, caja, imagen) {
    if (imagen) {
      cambiarCampo(idx, { x: caja.x, y: caja.y, w: caja.w, h: caja.h });
      return;
    }

    var tam = Math.min(caja.h, TAMANO_MAXIMO);
    var cambios = { x: caja.x, y: caja.y, w: caja.w, size: tam };

    // El minimo del ajuste "shrink" no puede quedar por encima del tamano, y
    // achicar la caja con el mouse es justo lo que lo deja por encima.
    var campo = campos[idx];
    if (campo && campo.minSize != null && campo.minSize > tam) cambios.minSize = tam;

    cambiarCampo(idx, cambios);
  }

  function calcular(g, dx, dy) {
    var c = g.caja;
    var techo = g.imagen ? 1 : TAMANO_MAXIMO;

    if (!g.asa) {
      return {
        x: limitar(c.x + dx, 0, Math.max(0, 1 - c.w)),
        y: limitar(c.y + dy, 0, Math.max(0, 1 - c.h)),
        w: c.w,
        h: c.h
      };
    }

    var x = c.x, y = c.y, w = c.w, h = c.h;

    if (g.asa.izq) {
      var nx = limitar(c.x + dx, 0, c.x + c.w - MINIMO);
      w = c.w + (c.x - nx);
      x = nx;
    }
    if (g.asa.der) {
      w = limitar(c.w + dx, MINIMO, 1 - c.x);
    }
    if (g.asa.arr) {
      var ny = limitar(c.y + dy, 0, c.y + c.h - MINIMO);
      h = limitar(c.h + (c.y - ny), MINIMO, techo);
      y = c.y + c.h - h;
    }
    if (g.asa.aba) {
      h = limitar(c.h + dy, MINIMO, Math.min(techo, 1 - c.y));
    }

    return { x: x, y: y, w: w, h: h };
  }

  function iniciar(e, idx, asa) {
    if (e.button !== undefined && e.button !== 0) return;
    e.preventDefault();
    e.stopPropagation();

    var lienzo = lienzoRef.current;
    if (!lienzo) return;

    var imagen = esImagen(campos[idx]);
    gesto.current = {
      idx: idx,
      asa: asa || null,
      imagen: imagen,
      rect: lienzo.getBoundingClientRect(),
      px: e.clientX,
      py: e.clientY,
      caja: cajaDe(campos[idx], imagen)
    };

    setSel(idx);
    try { e.currentTarget.setPointerCapture(e.pointerId); } catch (err) { /* sin captura */ }
  }

  function mover(e) {
    var g = gesto.current;
    if (!g || !g.rect.width || !g.rect.height) return;
    var dx = (e.clientX - g.px) / g.rect.width;
    var dy = (e.clientY - g.py) / g.rect.height;
    guardarCaja(g.idx, calcular(g, dx, dy), g.imagen);
  }

  function terminar(e) {
    if (!gesto.current) return;
    gesto.current = null;
    try { e.currentTarget.releasePointerCapture(e.pointerId); } catch (err) { /* ya liberado */ }
  }

  // Las flechas mueven de a poco lo que el mouse deja casi en su lugar. Sin
  // dependencias a proposito: se vuelve a enganchar en cada render para que
  // la funcion vea siempre el campo seleccionado actual.
  useEffect(function() {
    function alTeclado(e) {
      if (sel === null || !campos[sel]) return;

      var foco = document.activeElement;
      if (foco && (foco.tagName === 'INPUT' || foco.tagName === 'TEXTAREA' || foco.isContentEditable)) return;

      var paso = e.shiftKey ? 0.02 : 0.005;
      var direcciones = {
        ArrowLeft: [-paso, 0], ArrowRight: [paso, 0],
        ArrowUp: [0, -paso], ArrowDown: [0, paso]
      };

      if (direcciones[e.key]) {
        e.preventDefault();
        var imagen = esImagen(campos[sel]);
        var c = cajaDe(campos[sel], imagen);
        guardarCaja(sel, {
          x: limitar(c.x + direcciones[e.key][0], 0, Math.max(0, 1 - c.w)),
          y: limitar(c.y + direcciones[e.key][1], 0, Math.max(0, 1 - c.h)),
          w: c.w, h: c.h
        }, imagen);
        return;
      }

      if (e.key === 'Delete') {
        e.preventDefault();
        quitarCampo(sel);
      }
    }

    window.addEventListener('keydown', alTeclado);
    return function() { window.removeEventListener('keydown', alTeclado); };
  });

  /* ------------------------------------------------------------------ fondo */

  var subirFondo = async function(e) {
    var archivo = e.target.files[0];
    if (!archivo) return;
    setSubiendo(true);
    setError('');
    try {
      var fd = new FormData();
      fd.append('file', archivo);
      var res = await axios.post(endpoints.templateBackground, fd);

      // La proporcion viene de la imagen subida, no del tamano de papel: el
      // arte llega con la forma que llega y los campos se acomodan sobre lo
      // que el disenador realmente ve.
      cambiarCara(cara, { backgroundKey: res.data.backgroundKey, aspectRatio: res.data.aspectRatio });
      setFondos(function(previos) {
        var siguientes = Object.assign({}, previos);
        siguientes[cara] = res.data.backgroundUrl;
        return siguientes;
      });
    } catch (err) {
      setError(err.message);
    } finally {
      setSubiendo(false);
      e.target.value = '';
    }
  };

  var quitarFondo = function() {
    cambiarCara(cara, { backgroundKey: null });
    setFondos(function(previos) {
      var siguientes = Object.assign({}, previos);
      siguientes[cara] = '';
      return siguientes;
    });
  };

  var agregarReverso = function() {
    setCaras(function(previas) {
      return Object.assign({}, previas, { back: caraVacia(previas.front.aspectRatio) });
    });
    setCara('back');
    setSel(null);
  };

  var quitarReverso = function() {
    if (!confirm('Quitar el reverso y todos sus campos?')) return;
    setCaras(function(previas) { return Object.assign({}, previas, { back: null }); });
    setFondos(function(previos) { return Object.assign({}, previos, { back: '' }); });
    setCara('front');
    setSel(null);
  };

  /* ---------------------------------------------------------------- guardar */

  var guardar = async function() {
    setGuardando(true);
    setError('');
    setAviso('');
    try {
      await apiPut(endpoints.template(templateId), {
        name: nombre,
        pageSize: tamano,
        isDefault: porDefecto,
        layout: { front: limpiarCara(caras.front), back: limpiarCara(caras.back) }
      });
      navigate('/dashboard/templates');
    } catch (err) {
      setError(err.message || 'No se pudo guardar el diseno.');
    } finally {
      setGuardando(false);
    }
  };

  /* ------------------------------------------------------------------ vista */

  if (cargando) return <Box sx={{ display: 'flex', justifyContent: 'center', mt: 10 }}><CircularProgress /></Box>;
  if (!plantilla) return <Alert severity="error">Plantilla no encontrada.</Alert>;

  var fondo = fondos[cara];
  var seleccionado = sel !== null ? campos[sel] : null;

  return (
    <Box>
      <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 2, gap: 2, flexWrap: 'wrap' }}>
        <Box>
          <Typography variant="h4" fontWeight={700}>Diseno: {plantilla.name}</Typography>
          <Typography variant="body2" color="text.secondary">
            {plantilla.kind === 'credential' ? 'Credencial' : 'Certificado'} &middot; v{plantilla.version}
          </Typography>
        </Box>
        <Box sx={{ display: 'flex', gap: 1 }}>
          <Button variant="outlined" onClick={function() { navigate('/dashboard/templates'); }}>Volver</Button>
          <Button variant="contained" onClick={guardar} disabled={guardando}>
            {guardando ? 'Guardando...' : 'Guardar diseno'}
          </Button>
        </Box>
      </Box>

      {error && <Alert severity="error" sx={{ mb: 2 }} onClose={function() { setError(''); }}>{error}</Alert>}
      {aviso && <Alert severity="info" sx={{ mb: 2 }} onClose={function() { setAviso(''); }}>{aviso}</Alert>}

      <Box sx={{ display: 'flex', gap: 3, alignItems: 'flex-start', flexDirection: { xs: 'column', md: 'row' } }}>
        <Box sx={{ flex: 2, width: '100%', minWidth: 0 }}>
          <Tabs value={cara} onChange={function(e, v) { setCara(v); setSel(null); }} sx={{ mb: 1, minHeight: 40 }}>
            <Tab value="front" label="Anverso" sx={{ minHeight: 40 }} />
            {caras.back && <Tab value="back" label="Reverso" sx={{ minHeight: 40 }} />}
          </Tabs>

          <Paper
            ref={lienzoRef}
            variant="outlined"
            onPointerDown={function() { setSel(null); }}
            sx={{
              position: 'relative',
              width: '100%',
              aspectRatio: String(caraActual.aspectRatio || 1.5875),
              bgcolor: '#fff',
              backgroundImage: 'linear-gradient(45deg,#f0f0f0 25%,transparent 25%,transparent 75%,#f0f0f0 75%),'
                + 'linear-gradient(45deg,#f0f0f0 25%,transparent 25%,transparent 75%,#f0f0f0 75%)',
              backgroundSize: '16px 16px',
              backgroundPosition: '0 0, 8px 8px',
              overflow: 'hidden',
              touchAction: 'none',
              userSelect: 'none'
            }}
          >
            {fondo && (
              <Box
                component="img"
                src={fondo}
                alt=""
                draggable={false}
                sx={{ position: 'absolute', inset: 0, width: '100%', height: '100%', objectFit: 'fill', pointerEvents: 'none' }}
              />
            )}

            {!fondo && (
              <Box sx={{ position: 'absolute', inset: 0, display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center', pointerEvents: 'none' }}>
                <Iconify icon="eva:image-outline" width={40} sx={{ color: 'text.disabled', mb: 1 }} />
                <Typography variant="body2" color="text.secondary">Sin arte de fondo. Los campos se imprimen sobre blanco.</Typography>
              </Box>
            )}

            {campos.map(function(campo, idx) {
              return (
                <CampoEnLienzo
                  key={idx}
                  campo={campo}
                  idx={idx}
                  imagen={esImagen(campo)}
                  etiqueta={etiquetaDe(campo)}
                  seleccionado={sel === idx}
                  alto={medida.h}
                  tipografias={tipografias}
                  caja={cajaDe(campo, esImagen(campo))}
                  onIniciar={iniciar}
                  onMover={mover}
                  onTerminar={terminar}
                />
              );
            })}
          </Paper>

          <Typography variant="caption" color="text.secondary" sx={{ mt: 1, display: 'block' }}>
            Arrastra una caja para moverla y sus tiradores para redimensionarla. Con una seleccionada,
            las flechas la mueven de a poco (con Shift, mas), y Supr la elimina.
          </Typography>
        </Box>

        <Box sx={{ flex: 1, width: '100%', minWidth: 280, maxWidth: { md: 380 } }}>
          <TextField label="Nombre" value={nombre} onChange={function(e) { setNombre(e.target.value); }} fullWidth size="small" sx={{ mb: 2 }} />

          <TextField select label="Tamano de impresion" value={tamano} onChange={function(e) { setTamano(e.target.value); }} fullWidth size="small" sx={{ mb: 1 }}>
            {tamanos.map(function(t) { return <MenuItem key={t.code} value={t.code}>{t.label}</MenuItem>; })}
          </TextField>

          <FormControlLabel
            control={<Switch checked={porDefecto} onChange={function(e) { setPorDefecto(e.target.checked); }} />}
            label="Usar por defecto para este tipo"
            sx={{ mb: 1 }}
          />

          <Divider sx={{ my: 2 }} />

          <Typography variant="subtitle2" fontWeight={700} sx={{ mb: 1 }}>
            Arte del {cara === 'front' ? 'anverso' : 'reverso'}
          </Typography>
          <Box sx={{ display: 'flex', gap: 1, mb: 1 }}>
            <Button variant="outlined" component="label" size="small" fullWidth disabled={subiendo}
              startIcon={subiendo ? <CircularProgress size={14} /> : <Iconify icon="eva:upload-outline" width={16} />}>
              {subiendo ? 'Subiendo...' : (fondo ? 'Reemplazar' : 'Subir imagen')}
              <input type="file" accept="image/*" hidden onChange={subirFondo} />
            </Button>
            {fondo && <Button size="small" color="inherit" onClick={quitarFondo}>Quitar</Button>}
          </Box>
          <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mb: 2 }}>
            El lienzo toma la proporcion de la imagen ({(caraActual.aspectRatio || 0).toFixed(3)}), no la del papel.
          </Typography>

          {caras.back
            ? <Button size="small" color="inherit" onClick={quitarReverso} startIcon={<Iconify icon="eva:close-outline" width={16} />}>Quitar reverso</Button>
            : <Button size="small" onClick={agregarReverso} startIcon={<Iconify icon="eva:plus-fill" width={16} />}>Agregar reverso</Button>}

          <Divider sx={{ my: 2 }} />

          <TextField select size="small" label="Agregar campo" value="" fullWidth sx={{ mb: 2 }}
            onChange={function(e) { if (e.target.value) agregarCampo(e.target.value); }}>
            <MenuItem value="">Seleccionar...</MenuItem>
            {fuentes.map(function(f) {
              return <MenuItem key={f.code} value={f.code}>{f.label}{f.shape === 'image' ? ' (imagen)' : ''}</MenuItem>;
            })}
          </TextField>

          {campos.length === 0 && (
            <Typography variant="body2" color="text.secondary">
              Esta cara no tiene campos todavia.
            </Typography>
          )}

          {campos.map(function(campo, idx) {
            return (
              <Paper key={idx} variant="outlined"
                onClick={function() { setSel(idx); }}
                sx={{ p: 1, mb: 1, cursor: 'pointer', borderColor: sel === idx ? 'primary.main' : undefined, borderWidth: sel === idx ? 2 : 1 }}>
                <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
                  <Iconify icon={esImagen(campo) ? 'eva:image-outline' : 'eva:text-outline'} width={16} sx={{ color: 'text.secondary', flexShrink: 0 }} />
                  <Typography variant="body2" fontWeight={500} noWrap sx={{ flexGrow: 1, minWidth: 0 }}>{etiquetaDe(campo)}</Typography>
                  <Tooltip title="Eliminar">
                    <IconButton size="small" onClick={function(e) { e.stopPropagation(); quitarCampo(idx); }}>
                      <Iconify icon="eva:close-outline" width={16} />
                    </IconButton>
                  </Tooltip>
                </Box>
              </Paper>
            );
          })}

          {seleccionado && (
            <Propiedades
              campo={seleccionado}
              imagen={esImagen(seleccionado)}
              tipografias={tipografias}
              alineaciones={(catalogo && catalogo.alignments) || []}
              ajustes={(catalogo && catalogo.fits) || []}
              onCambios={function(cambios) { cambiarCampo(sel, cambios); }}
            />
          )}
        </Box>
      </Box>
    </Box>
  );
}

/* -------------------------------------------------------------------------
   Una caja sobre el lienzo.

   El texto se dibuja con el alto que va a tener impreso: `size` es una
   fraccion del alto de la cara, asi que en pantalla son `size * altoDelLienzo`
   pixeles. Es la unica forma de que lo que se acomoda sea lo que sale.
   ------------------------------------------------------------------------- */

function CampoEnLienzo(props) {
  var campo = props.campo;
  var caja = props.caja;
  var imagen = props.imagen;
  var sel = props.seleccionado;

  var familia = 'inherit';
  if (!imagen) {
    var t = props.tipografias.find(function(f) { return f.code === (campo.font || 'sans'); });
    if (t) familia = t.family;
  }

  var alineacion = campo.align || 'left';

  return (
    <Box
      onPointerDown={function(e) { props.onIniciar(e, props.idx, null); }}
      onPointerMove={props.onMover}
      onPointerUp={props.onTerminar}
      onPointerCancel={props.onTerminar}
      sx={{
        position: 'absolute',
        left: (caja.x * 100) + '%',
        top: (caja.y * 100) + '%',
        width: (caja.w * 100) + '%',
        height: (caja.h * 100) + '%',
        cursor: 'move',
        touchAction: 'none',
        outline: sel ? '2px solid #1976d2' : '1px dashed rgba(25,118,210,0.55)',
        outlineOffset: 0,
        bgcolor: sel ? 'rgba(25,118,210,0.10)' : 'rgba(25,118,210,0.04)',
        boxSizing: 'border-box'
      }}
    >
      {imagen ? (
        <Box sx={{ width: '100%', height: '100%', display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center', overflow: 'hidden', pointerEvents: 'none' }}>
          <Iconify icon={campo.source === 'document.qr' ? 'eva:grid-outline' : 'eva:person-outline'} width={18} sx={{ color: 'primary.main', opacity: 0.7 }} />
          <Typography variant="caption" sx={{ fontSize: 9, color: 'primary.main', lineHeight: 1.1, textAlign: 'center' }} noWrap>
            {props.etiqueta}
          </Typography>
        </Box>
      ) : (
        <Box
          sx={{
            width: '100%', height: '100%', display: 'flex', alignItems: 'flex-start',
            justifyContent: alineacion === 'center' ? 'center' : alineacion === 'right' ? 'flex-end' : 'flex-start',
            overflow: 'hidden', pointerEvents: 'none',
            fontFamily: familia,
            fontWeight: campo.bold ? 700 : 400,
            color: campo.color || '#000',
            fontSize: Math.max(6, (campo.size || 0.06) * (props.alto || 0)) + 'px',
            lineHeight: 1,
            whiteSpace: 'nowrap'
          }}
        >
          {props.etiqueta}
        </Box>
      )}

      {sel && ASAS.map(function(asa) {
        return (
          <Box
            key={asa.id}
            onPointerDown={function(e) { props.onIniciar(e, props.idx, asa); }}
            onPointerMove={props.onMover}
            onPointerUp={props.onTerminar}
            onPointerCancel={props.onTerminar}
            sx={{
              position: 'absolute',
              left: (asa.cx * 100) + '%',
              top: (asa.cy * 100) + '%',
              width: 10, height: 10, ml: '-5px', mt: '-5px',
              bgcolor: '#fff',
              border: '2px solid #1976d2',
              borderRadius: '2px',
              cursor: asa.cursor,
              touchAction: 'none'
            }}
          />
        );
      })}
    </Box>
  );
}

/* ------------------------------------------------------------ propiedades */

function Propiedades(props) {
  var campo = props.campo;
  var imagen = props.imagen;

  var num = function(prop, etiqueta, paso, min, max) {
    return (
      <TextField
        label={etiqueta}
        type="number"
        size="small"
        value={campo[prop] != null ? campo[prop] : ''}
        onChange={function(e) {
          var v = e.target.value === '' ? null : Number(e.target.value);
          props.onCambios(cambio(prop, v));
        }}
        slotProps={{ htmlInput: { step: paso, min: min, max: max } }}
        sx={{ flex: 1 }}
      />
    );
  };

  function cambio(prop, valor) {
    var c = {};
    c[prop] = valor;
    return c;
  }

  return (
    <Paper variant="outlined" sx={{ p: 1.5, mt: 2, display: 'flex', flexDirection: 'column', gap: 1.5 }}>
      <Typography variant="subtitle2" fontWeight={700}>Propiedades</Typography>

      <Box sx={{ display: 'flex', gap: 1 }}>
        {num('x', 'X', 0.005, 0, 1)}
        {num('y', 'Y', 0.005, 0, 1)}
      </Box>

      <Box sx={{ display: 'flex', gap: 1 }}>
        {num('w', 'Ancho', 0.005, MINIMO, 1)}
        {imagen ? num('h', 'Alto', 0.005, MINIMO, 1) : num('size', 'Alto del texto', 0.005, 0.005, TAMANO_MAXIMO)}
      </Box>

      {campo.source === 'text' && (
        <TextField
          label="Texto"
          size="small"
          value={campo.text || ''}
          onChange={function(e) { props.onCambios({ text: e.target.value }); }}
          slotProps={{ htmlInput: { maxLength: 120 } }}
          fullWidth
        />
      )}

      {!imagen && (
        <>
          <Box sx={{ display: 'flex', gap: 1 }}>
            <TextField select label="Tipografia" size="small" value={campo.font || 'sans'}
              onChange={function(e) { props.onCambios({ font: e.target.value }); }} sx={{ flex: 1 }}>
              {props.tipografias.map(function(f) { return <MenuItem key={f.code} value={f.code}>{f.label}</MenuItem>; })}
            </TextField>
            <TextField select label="Alineacion" size="small" value={campo.align || 'left'}
              onChange={function(e) { props.onCambios({ align: e.target.value }); }} sx={{ flex: 1 }}>
              {props.alineaciones.map(function(a) {
                return <MenuItem key={a} value={a}>{a === 'left' ? 'Izquierda' : a === 'center' ? 'Centro' : 'Derecha'}</MenuItem>;
              })}
            </TextField>
          </Box>

          <Box sx={{ display: 'flex', gap: 1, alignItems: 'center' }}>
            <TextField select label="Si no entra" size="small" value={campo.fit || 'shrink'}
              onChange={function(e) { props.onCambios({ fit: e.target.value }); }} sx={{ flex: 1 }}>
              {props.ajustes.map(function(a) {
                return <MenuItem key={a} value={a}>{a === 'shrink' ? 'Achicar' : a === 'wrap' ? 'Dos lineas' : 'Cortar'}</MenuItem>;
              })}
            </TextField>
            <TextField label="Color" type="color" size="small" value={campo.color || '#000000'}
              onChange={function(e) { props.onCambios({ color: e.target.value }); }} sx={{ width: 84 }} />
          </Box>

          {(campo.fit || 'shrink') === 'shrink' && (
            <TextField
              label="No achicar por debajo de"
              type="number"
              size="small"
              value={campo.minSize != null ? campo.minSize : ''}
              onChange={function(e) { props.onCambios({ minSize: e.target.value === '' ? null : Number(e.target.value) }); }}
              slotProps={{ htmlInput: { step: 0.005, min: 0.005, max: campo.size || TAMANO_MAXIMO } }}
              helperText="Vacio, achica lo que haga falta."
              fullWidth
            />
          )}

          <FormControlLabel
            control={<Switch size="small" checked={!!campo.bold} onChange={function(e) { props.onCambios({ bold: e.target.checked }); }} />}
            label="Negrita"
          />
        </>
      )}
    </Paper>
  );
}

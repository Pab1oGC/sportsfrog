import { useParams, useNavigate } from 'react-router';
import AppBar from '@mui/material/AppBar';
import Toolbar from '@mui/material/Toolbar';
import Box from '@mui/material/Box';
import Container from '@mui/material/Container';
import Card from '@mui/material/Card';
import CardContent from '@mui/material/CardContent';
import Avatar from '@mui/material/Avatar';
import Typography from '@mui/material/Typography';
import Chip from '@mui/material/Chip';
import Divider from '@mui/material/Divider';
import Stack from '@mui/material/Stack';
import Alert from '@mui/material/Alert';
import CircularProgress from '@mui/material/CircularProgress';
import { alpha } from '@mui/material/styles';
import useSWR from 'swr';
import { publicFetcher } from 'src/lib/public-axios';
import { useApi } from 'src/hooks/use-api';
import { endpoints } from 'src/lib/axios';
import { useAuthContext } from 'src/auth/hooks';
import { Iconify } from 'src/components/iconify';
import { EmptyState } from 'src/components/empty-state';

// Lo que pinta cada estado -- los cuatro de VerificationState (ver
// VerifyDocument.cs del lado del API) más "blocked", que ese mismo enum
// suma cuando una sanción sin resolver es la razón, nunca la fecha ni una
// revocación. Un mapa en vez de un switch -- si la API alguna vez suma un
// estado, es una entrada nueva acá y no una rama más en el componente que
// lo dibuja.
const STATUS_PRESENTATION = {
  valid: { label: 'Válida', color: 'success', icon: 'eva:checkmark-circle-2-fill' },
  revoked: { label: 'Revocada', color: 'error', icon: 'eva:close-circle-outline' },
  expired: { label: 'Vencida', color: 'warning', icon: 'eva:alert-triangle-outline' },
  not_yet_valid: { label: 'Todavía no vigente', color: 'warning', icon: 'eva:alert-triangle-outline' },
  blocked: { label: 'No habilitada', color: 'error', icon: 'mdi:shield-off-outline' },
};

const KIND_LABEL = { credential: 'Credencial', certificate: 'Certificado' };

const COMPLETION_RULE_LABEL = { matches: 'Partidos', fine: 'Multa', both: 'Partidos y multa' };

// DateOnly llega como "2026-10-17". new Date() de un string sin hora lo lee
// en UTC medianoche, que en cualquier huso al oeste de UTC muestra el día
// anterior -- T00:00:00 fuerza la lectura en hora local.
function formatearFecha(valor) {
  if (!valor) return null;
  return new Date(valor + 'T00:00:00').toLocaleDateString('es-BO', {
    day: '2-digit', month: 'short', year: 'numeric',
  });
}

/**
 * Lo que pinta el QR de cada documento. Dos respuestas distintas sobre la
 * misma dirección, según quien lo escanea:
 *
 * - Sin sesión: VerifyDocument.cs, anónimo, acotado a lo que la propia
 *   tarjeta ya dice (RNF-16) -- "no habilitada" cuando hay una sanción sin
 *   resolver, nunca por qué.
 * - Con sesión de la organización: ScanCredential.cs, con la foto, el
 *   motivo de una revocación y el detalle completo de la sanción -- lo que
 *   un steward en la puerta necesita para decidir algo, no solo para
 *   confirmarlo.
 *
 * `authLoading` decide cuál de las dos pedir recién cuando se sabe si hay
 * sesión o no -- pedir la pública de entrada y cambiar después haría
 * parpadear la respuesta correcta en pantalla.
 */
export default function VerifyDocumentPage() {
  const { orgSlug, serial } = useParams();
  const navigate = useNavigate();
  const { authenticated, loading: authLoading } = useAuthContext();

  const publicResult = useSWR(
    !authLoading && !authenticated && orgSlug && serial
      ? `/api/public/verify/${orgSlug}/${encodeURIComponent(serial)}`
      : null,
    publicFetcher,
  );

  const staffResult = useApi(
    !authLoading && authenticated && serial ? endpoints.scanCredential(encodeURIComponent(serial)) : null,
  );

  const { data, error } = authenticated ? staffResult : publicResult;
  const isLoading = authLoading || publicResult.isLoading || staffResult.isLoading;

  // Un serial que no existe y cualquier otro fallo al resolverlo se tratan
  // igual a propósito: la API ya responde lo mismo para los dos (404) para
  // no delatar qué distingue "no existe" de "existe pero no es tuyo" --
  // este componente no vuelve a abrir esa distinción del lado del cliente.
  const sinResultado = !isLoading && (error || !data);

  return (
    <Box sx={{ minHeight: '100vh', display: 'flex', flexDirection: 'column', bgcolor: 'background.default' }}>
      <Navbar onLogoClick={() => navigate('/')} />
      <Container maxWidth="sm" sx={{ flexGrow: 1, display: 'flex', alignItems: 'center', py: 4 }}>
        <Card sx={{ width: '100%' }}>
          <CardContent sx={{ p: { xs: 3, sm: 4 } }}>
            {isLoading && (
              <Box sx={{ display: 'flex', justifyContent: 'center', py: 4 }}>
                <CircularProgress />
              </Box>
            )}

            {!isLoading && sinResultado && (
              <EmptyState icon="mdi:alert-circle-outline">
                Este código no corresponde a ningún documento emitido. Si lo escaneaste de una tarjeta impresa,
                puede que la dirección de verificación ya no esté activa.
              </EmptyState>
            )}

            {!isLoading && !sinResultado && authenticated && <ResultadoStaff data={data} />}
            {!isLoading && !sinResultado && !authenticated && <ResultadoPublico data={data} />}
          </CardContent>
        </Card>
      </Container>
    </Box>
  );
}

// Un documento resuelto para quien lo escanea sin sesión: lo que dice la
// tarjeta (nombre y foto ya están impresos, así que no hace falta
// repetirlos) más lo que la tarjeta no puede mostrar por sí sola -- si
// sigue siendo válida hoy.
function ResultadoPublico({ data }) {
  const presentacion = STATUS_PRESENTATION[data.status] || STATUS_PRESENTATION.revoked;
  const esCredencial = data.kind === 'credential';
  const vigenciaDesde = formatearFecha(data.validFrom);
  const vigenciaHasta = formatearFecha(data.validTo);

  return (
    <Box>
      <EstadoCircular presentacion={presentacion} />
      <Box sx={{ textAlign: 'center', mb: 3 }}>
        <Typography variant="h6" fontWeight={700} sx={{ mt: 1.5 }}>{data.holder}</Typography>
        <Typography variant="body2" color="text.secondary">
          {KIND_LABEL[data.kind] || data.kind} · {data.organization}
        </Typography>
      </Box>

      <Divider sx={{ mb: 2 }} />

      <Stack spacing={1.25}>
        {esCredencial && data.visibleId && <Campo label="Identificador" valor={data.visibleId} />}
        {data.club && <Campo label="Club" valor={data.club} />}
        {data.team && <Campo label="Equipo" valor={data.team} />}
        {data.category && <Campo label="Categoría" valor={data.category} />}
        <Campo label="Competencia" valor={`${data.competition} ${data.season}`} />
        {data.certificateType && <Campo label="Motivo" valor={data.certificateType} />}
        {(vigenciaDesde || vigenciaHasta) && (
          <Campo label="Vigencia" valor={`${vigenciaDesde || 'Sin inicio'} – ${vigenciaHasta || 'sin fin'}`} />
        )}
        <Campo label="Emitido" valor={formatearFecha(data.issuedOn)} />
        {/* El motivo de la revocación es deliberadamente interno (ver
            VerifyDocument.cs) -- solo la fecha se muestra acá, y nada en
            absoluto cuando el motivo es una sanción (status "blocked"). */}
        {data.status === 'revoked' && data.revokedOn && (
          <Campo label="Revocado" valor={formatearFecha(data.revokedOn)} />
        )}
      </Stack>

      <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mt: 3, textAlign: 'center' }}>
        Serie {data.serialNumber}
      </Typography>
    </Box>
  );
}

// La misma tarjeta, leída con sesión de la organización: la foto para
// comparar contra la cara que la presenta, por qué una revocación pasó, y
// el detalle de cualquier sanción sin resolver -- no solo que hay una.
function ResultadoStaff({ data }) {
  const presentacion = data.entryBlocked && data.documentStatus === 'valid'
    ? STATUS_PRESENTATION.blocked
    : (STATUS_PRESENTATION[data.documentStatus] || STATUS_PRESENTATION.revoked);
  const esCredencial = data.kind === 'credential';
  const holder = [data.holderFirstName, data.holderLastName].filter(Boolean).join(' ');
  const vigenciaDesde = formatearFecha(data.validFrom);
  const vigenciaHasta = formatearFecha(data.validTo);

  return (
    <Box>
      {data.photoUrl ? (
        <Box sx={{ display: 'flex', justifyContent: 'center', mb: 1.5 }}>
          <Avatar src={data.photoUrl} variant="rounded" sx={{ width: 96, height: 96, border: '2px solid', borderColor: 'divider' }} />
        </Box>
      ) : (
        <EstadoCircular presentacion={presentacion} />
      )}

      <Box sx={{ textAlign: 'center', mb: 2 }}>
        {data.photoUrl && (
          <Chip label={presentacion.label} color={presentacion.color} sx={{ fontWeight: 700, fontSize: 15, px: 1, mb: 1 }} />
        )}
        <Typography variant="h6" fontWeight={700} sx={{ mt: data.photoUrl ? 0.5 : 1.5 }}>
          {holder || data.team || 'Sin nombre'}
        </Typography>
        <Typography variant="body2" color="text.secondary">
          {KIND_LABEL[data.kind] || data.kind} · {data.competition} {data.season}
        </Typography>
      </Box>

      {data.entryBlocked && (
        <Alert severity="error" sx={{ mb: 2 }}>
          No habilitada para ingresar.
        </Alert>
      )}

      <Divider sx={{ mb: 2 }} />

      <Stack spacing={1.25}>
        {esCredencial && data.visibleId && <Campo label="Identificador" valor={data.visibleId} />}
        {data.club && <Campo label="Club" valor={data.club} />}
        {data.team && <Campo label="Equipo" valor={data.team} />}
        {data.category && <Campo label="Categoría" valor={data.category} />}
        {data.certificateType && <Campo label="Motivo" valor={data.certificateType} />}
        {(vigenciaDesde || vigenciaHasta) && (
          <Campo label="Vigencia" valor={`${vigenciaDesde || 'Sin inicio'} – ${vigenciaHasta || 'sin fin'}`} />
        )}
        <Campo label="Emitido" valor={formatearFecha(data.issuedOn)} />
        {data.documentStatus === 'revoked' && (
          <>
            <Campo label="Revocado" valor={formatearFecha(data.revokedOn)} />
            {data.revocationReason && <Campo label="Motivo de revocación" valor={data.revocationReason} />}
          </>
        )}
      </Stack>

      {data.activeSanctions.length > 0 && (
        <Box sx={{ mt: 2.5 }}>
          <Typography variant="subtitle2" fontWeight={700} sx={{ mb: 1 }}>
            Sanciones sin resolver
          </Typography>
          <Stack spacing={1}>
            {data.activeSanctions.map((sancion) => <SancionActiva key={sancion.id} sancion={sancion} />)}
          </Stack>
        </Box>
      )}

      <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mt: 3, textAlign: 'center' }}>
        Serie {data.serialNumber}
      </Typography>
    </Box>
  );
}

function SancionActiva({ sancion }) {
  return (
    <Box sx={{ p: 1.5, border: '1px solid', borderColor: 'error.main', borderRadius: 1, bgcolor: (t) => alpha(t.palette.error.main, 0.06) }}>
      <Typography variant="body2" fontWeight={600}>{sancion.reason}</Typography>
      <Stack direction="row" spacing={1} flexWrap="wrap" sx={{ mt: 0.75 }}>
        <Chip size="small" variant="outlined" label={COMPLETION_RULE_LABEL[sancion.completionRule] || sancion.completionRule} />
        {sancion.matchesRemaining != null && (
          <Chip size="small" color="warning" label={`Faltan ${sancion.matchesRemaining} partido(s)`} />
        )}
        {sancion.fineBalance != null && sancion.fineBalance > 0 && (
          <Chip size="small" color="warning" label={`Saldo Bs ${sancion.fineBalance}`} />
        )}
        {sancion.credentialWithheld && (
          <Chip size="small" color="error" label="Credencial retenida" />
        )}
      </Stack>
    </Box>
  );
}

function EstadoCircular({ presentacion }) {
  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 1, mb: 1.5 }}>
      <Box
        sx={{
          width: 72, height: 72, borderRadius: '50%', display: 'flex', alignItems: 'center',
          justifyContent: 'center',
          bgcolor: (theme) => alpha(theme.palette[presentacion.color].main, 0.12),
        }}
      >
        <Iconify icon={presentacion.icon} width={40} sx={{ color: `${presentacion.color}.main` }} />
      </Box>
      <Chip label={presentacion.label} color={presentacion.color} sx={{ fontWeight: 700, fontSize: 15, px: 1 }} />
    </Box>
  );
}

function Campo({ label, valor }) {
  return (
    <Box sx={{ display: 'flex', justifyContent: 'space-between', gap: 2 }}>
      <Typography variant="body2" color="text.secondary">{label}</Typography>
      <Typography variant="body2" fontWeight={600} sx={{ textAlign: 'right' }}>{valor}</Typography>
    </Box>
  );
}

function Navbar({ onLogoClick }) {
  return (
    <AppBar position="static" elevation={0} sx={{ bgcolor: 'background.paper', color: 'text.primary', borderBottom: '1px solid', borderColor: 'divider' }}>
      <Toolbar>
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, cursor: 'pointer' }} onClick={onLogoClick}>
          <Box sx={{ width: 36, height: 36, borderRadius: 1.5, bgcolor: 'primary.main', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
            <Typography sx={{ color: 'primary.contrastText', fontWeight: 800, fontSize: 16 }}>SF</Typography>
          </Box>
          <Typography variant="h6" fontWeight={700}>SportFrog</Typography>
        </Box>
      </Toolbar>
    </AppBar>
  );
}

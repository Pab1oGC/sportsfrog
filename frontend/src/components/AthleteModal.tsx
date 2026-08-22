import { CheckCircle2, ShieldAlert, Upload, User, X } from 'lucide-react';
import { useEffect, useMemo, useState } from 'react';
import type { Team } from '../types';

export type BoliviaExtension = 'CB' | 'LP' | 'SC' | 'OR' | 'PT' | 'TJ' | 'CH' | 'BN' | 'PA';

export interface AthleteFormValues {
  firstName: string;
  lastName: string;
  documentId: string;
  documentExtension: BoliviaExtension;
  birthDate: string;
  gender: 'Masculino' | 'Femenino' | 'Mixto';
  guardianName: string;
  guardianPhone: string;
  teamId: number | null;
  jerseyNumber: number | null;
  position: string;
  photo: File | null;
}

interface AthleteModalProps {
  open: boolean;
  teams: Team[];
  loading?: boolean;
  error?: string | null;
  onClose: () => void;
  onSave: (values: AthleteFormValues) => void | Promise<void>;
}

const extensions: BoliviaExtension[] = ['CB', 'LP', 'SC', 'OR', 'PT', 'TJ', 'CH', 'BN', 'PA'];
const initialForm: AthleteFormValues = {
  firstName: '', lastName: '', documentId: '', documentExtension: 'CB', birthDate: '',
  gender: 'Masculino', guardianName: '', guardianPhone: '', teamId: null, jerseyNumber: null,
  position: '', photo: null,
};

function calculateAge(birthDate: string) {
  if (!birthDate) return null;
  const today = new Date();
  const birth = new Date(`${birthDate}T00:00:00`);
  let age = today.getFullYear() - birth.getFullYear();
  const monthDelta = today.getMonth() - birth.getMonth();
  if (monthDelta < 0 || (monthDelta === 0 && today.getDate() < birth.getDate())) age -= 1;
  return age >= 0 ? age : null;
}

export function AthleteModal({ open, teams, loading = false, error, onClose, onSave }: AthleteModalProps) {
  const [form, setForm] = useState(initialForm);
  const [localError, setLocalError] = useState<string | null>(null);
  const [previewUrl, setPreviewUrl] = useState<string | null>(null);
  const age = useMemo(() => calculateAge(form.birthDate), [form.birthDate]);
  const isMinor = age !== null && age < 18;

  useEffect(() => {
    if (!open) return;
    setForm(initialForm);
    setLocalError(null);
    setPreviewUrl(null);
  }, [open]);

  useEffect(() => () => { if (previewUrl) URL.revokeObjectURL(previewUrl); }, [previewUrl]);

  if (!open) return null;

  const update = <K extends keyof AthleteFormValues>(key: K, value: AthleteFormValues[K]) => {
    setForm((current) => ({ ...current, [key]: value }));
    setLocalError(null);
  };

  const handlePhoto = (file: File | undefined) => {
    if (!file) return;
    if (!file.type.startsWith('image/')) {
      setLocalError('Selecciona una imagen válida.');
      return;
    }
    if (previewUrl) URL.revokeObjectURL(previewUrl);
    setPreviewUrl(URL.createObjectURL(file));
    update('photo', file);
  };

  const handleSubmit = async () => {
    if (!form.photo) return setLocalError('La fotografía es obligatoria para emitir la credencial.');
    if (!form.firstName.trim() || !form.lastName.trim() || !form.documentId.trim() || !form.birthDate) {
      return setLocalError('Completa nombre, apellido, documento y fecha de nacimiento.');
    }
    if (isMinor && (!form.guardianName.trim() || !form.guardianPhone.trim())) {
      return setLocalError('Los datos del tutor son obligatorios para menores de edad.');
    }
    if (form.teamId !== null && (form.jerseyNumber === null || !form.position.trim())) {
      return setLocalError('Completa dorsal y posición cuando asignes un equipo.');
    }
    await onSave(form);
  };

  return (
    <div className="fixed inset-0 z-40 flex items-center justify-center bg-slate-950/50 p-4 backdrop-blur-sm" role="dialog" aria-modal="true" aria-labelledby="athlete-modal-title">
      <div className="max-h-[90vh] w-full max-w-3xl overflow-y-auto rounded-2xl bg-white p-6 shadow-2xl">
        <div className="mb-6 flex items-start justify-between">
          <div>
            <p className="text-xs font-semibold uppercase tracking-[0.2em] text-slate-500">Registro deportivo</p>
            <h3 id="athlete-modal-title" className="mt-1 text-2xl font-bold text-slate-900">Crear jugador</h3>
          </div>
          <button type="button" onClick={onClose} aria-label="Cerrar" className="rounded-full p-2 text-slate-500 transition hover:bg-slate-100 hover:text-slate-900"><X className="h-5 w-5" /></button>
        </div>

        <div className="grid gap-6 lg:grid-cols-[180px_1fr]">
          <div>
            <label htmlFor="athlete-photo" className="group relative flex aspect-[3/4] cursor-pointer flex-col items-center justify-center overflow-hidden rounded-2xl border-2 border-dashed border-slate-300 bg-slate-50 text-center transition hover:border-[#00ACD8] hover:bg-cyan-50">
              {previewUrl ? <img src={previewUrl} alt="Vista previa del jugador" className="h-full w-full object-cover" /> : <><User className="h-10 w-10 text-slate-400" /><span className="mt-3 px-4 text-sm font-semibold text-slate-600">Subir foto 3x4</span><Upload className="mt-2 h-4 w-4 text-[#006098]" /></>}
              {previewUrl && <span className="absolute inset-x-2 bottom-2 rounded-lg bg-slate-950/75 px-2 py-1.5 text-center text-xs font-semibold text-white opacity-0 transition group-hover:opacity-100">Cambiar fotografía</span>}
            </label>
            <input id="athlete-photo" type="file" accept="image/*" className="sr-only" onChange={(event) => handlePhoto(event.target.files?.[0])} />
            <p className="mt-3 text-xs leading-5 text-amber-800"><ShieldAlert className="mr-1 inline h-3.5 w-3.5" />Indispensable para emitir la credencial.</p>
          </div>

          <div className="space-y-5">
            <div className="grid gap-4 md:grid-cols-2">
              <Field label="Nombres *" value={form.firstName} onChange={(value) => update('firstName', value)} />
              <Field label="Apellidos *" value={form.lastName} onChange={(value) => update('lastName', value)} />
              <label className="block text-sm md:col-span-2"><span className="mb-1.5 block font-medium text-slate-600">Documento de identidad *</span><div className="flex"><input value={form.documentId} onChange={(event) => update('documentId', event.target.value)} className="min-w-0 flex-1 rounded-l-xl border border-r-0 border-slate-200 bg-slate-50 px-3 py-2.5 outline-none focus:border-[#00ACD8] focus:bg-white" placeholder="1234567" /><select value={form.documentExtension} onChange={(event) => update('documentExtension', event.target.value as BoliviaExtension)} className="rounded-r-xl border border-slate-200 bg-white px-3 py-2.5 font-semibold text-[#006098] outline-none focus:ring-2 focus:ring-[#00ACD8]">{extensions.map((extension) => <option key={extension}>{extension}</option>)}</select></div></label>
              <Field label="Fecha de nacimiento *" type="date" value={form.birthDate} onChange={(value) => update('birthDate', value)} />
              <label className="block text-sm"><span className="mb-1.5 block font-medium text-slate-600">Género / Rama *</span><select value={form.gender} onChange={(event) => update('gender', event.target.value as AthleteFormValues['gender'])} className="w-full rounded-xl border border-slate-200 bg-slate-50 px-3 py-2.5 outline-none focus:border-[#00ACD8] focus:bg-white"><option>Masculino</option><option>Femenino</option><option>Mixto</option></select></label>
            </div>

            {age !== null && <div className="flex items-center gap-2 rounded-xl border border-slate-200 bg-slate-50 px-3 py-2 text-sm text-slate-700"><CheckCircle2 className="h-4 w-4 text-emerald-600" />Edad calculada: <strong>{age} años</strong></div>}

            {isMinor && <div className="rounded-xl border border-amber-200 bg-amber-50 p-4 text-amber-800"><div className="flex items-start gap-2"><ShieldAlert className="mt-0.5 h-5 w-5 shrink-0" /><div><strong>Autorización de tutor requerida</strong><p className="mt-1 text-sm">Este jugador es menor de edad. Completa los datos para continuar.</p></div></div><div className="mt-4 grid gap-4 md:grid-cols-2"><Field label="Nombre completo del tutor *" value={form.guardianName} onChange={(value) => update('guardianName', value)} /><Field label="Teléfono / WhatsApp *" value={form.guardianPhone} onChange={(value) => update('guardianPhone', value)} /></div></div>}

            <details className="group rounded-xl border border-slate-200 bg-slate-50 p-4"><summary className="cursor-pointer text-sm font-semibold text-slate-700">Inscribir en una nómina ahora (opcional)</summary><div className="mt-4 grid gap-4 md:grid-cols-3"><label className="block text-sm md:col-span-3"><span className="mb-1.5 block font-medium text-slate-600">Equipo</span><select value={form.teamId ?? ''} onChange={(event) => update('teamId', event.target.value ? Number(event.target.value) : null)} className="w-full rounded-xl border border-slate-200 bg-white px-3 py-2.5 outline-none focus:border-[#00ACD8]"><option value="">Sin equipo por ahora</option>{teams.map((team) => <option key={team.id} value={team.id}>{team.name}</option>)}</select></label><Field label="Dorsal (0-99)" type="number" value={form.jerseyNumber?.toString() ?? ''} onChange={(value) => update('jerseyNumber', value === '' ? null : Math.max(0, Math.min(99, Number(value))))} /><Field label="Posición" value={form.position} onChange={(value) => update('position', value)} /></div></details>
          </div>
        </div>

        {(localError || error) && <div className="mt-5 rounded-xl border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">{localError || error}</div>}
        <div className="mt-6 flex justify-end gap-3 border-t border-slate-200 pt-5"><button type="button" onClick={onClose} className="rounded-xl border border-slate-200 px-4 py-2.5 text-sm font-medium text-slate-600 hover:bg-slate-50">Cancelar</button><button type="button" disabled={loading} onClick={() => void handleSubmit()} className="rounded-xl bg-[#006098] px-5 py-2.5 text-sm font-semibold text-white transition hover:-translate-y-0.5 hover:bg-[#006098]/90 disabled:cursor-not-allowed disabled:opacity-60">{loading ? 'Guardando...' : 'Guardar jugador'}</button></div>
      </div>
    </div>
  );
}

function Field({ label, value, onChange, type = 'text' }: { label: string; value: string; onChange: (value: string) => void; type?: 'text' | 'date' | 'number' }) {
  return <label className="block text-sm"><span className="mb-1.5 block font-medium text-slate-600">{label}</span><input type={type} min={type === 'number' ? 0 : undefined} max={type === 'number' ? 99 : undefined} value={value} onChange={(event) => onChange(event.target.value)} className="w-full rounded-xl border border-slate-200 bg-slate-50 px-3 py-2.5 outline-none focus:border-[#00ACD8] focus:bg-white" /></label>;
}

import { describe, expect, it } from 'vitest';
import { ESTADO_FOTO, detalleDeFoto, estadoDeFoto, mensajeDeError } from 'src/lib/photo-validation';

describe('estadoDeFoto', () => {
  it('reconoce los tres estados que responde la API', () => {
    expect(estadoDeFoto({ state: 'approved' })).toBe('approved');
    expect(estadoDeFoto({ state: 'rejected' })).toBe('rejected');
    expect(estadoDeFoto({ state: 'not_evaluated' })).toBe('not_evaluated');
  });

  it('trata un veredicto ausente como sin evaluar, nunca como aprobada', () => {
    expect(estadoDeFoto(undefined)).toBe('not_evaluated');
    expect(estadoDeFoto(null)).toBe('not_evaluated');
  });

  it('trata un estado desconocido como sin evaluar', () => {
    expect(estadoDeFoto({ state: 'revisar' })).toBe('not_evaluated');
  });

  it('tiene una etiqueta y un color para cada estado', () => {
    for (const estado of Object.keys(ESTADO_FOTO)) {
      expect(ESTADO_FOTO[estado].label).toBeTruthy();
      expect(ESTADO_FOTO[estado].color).toBeTruthy();
    }
  });
});

describe('detalleDeFoto', () => {
  it('separa lo que bloquea, lo que sólo advierte y lo que no se revisó', () => {
    const detalle = detalleDeFoto({
      reasons: ['Se ven lentes de sol.'],
      warnings: ['La foto está borrosa.'],
      unverified: ['No verificado: boca cerrada.'],
    });

    expect(detalle.motivos).toEqual(['Se ven lentes de sol.']);
    expect(detalle.advertencias).toEqual(['La foto está borrosa.']);
    expect(detalle.noVerificado).toEqual(['No verificado: boca cerrada.']);
  });

  it('devuelve listas vacías cuando no hay veredicto', () => {
    expect(detalleDeFoto(undefined)).toEqual({
      motivos: [], advertencias: [], noVerificado: [],
    });
  });
});

describe('mensajeDeError', () => {
  it('pide reintentar cuando el validador no respondió', () => {
    const error = { response: { status: 503, data: { detail: 'interno' } } };

    expect(mensajeDeError(error)).toMatch(/Intentá de nuevo/);
  });

  it('muestra el error de la foto cuando la API lo rechazó como no legible', () => {
    const error = {
      response: {
        status: 422,
        data: { errors: { archivo: ['La foto no se pudo leer como una imagen.'] } },
      },
    };

    expect(mensajeDeError(error)).toBe('La foto no se pudo leer como una imagen.');
  });

  it('usa el detalle del problema cuando lo hay', () => {
    const error = { response: { status: 500, data: { detail: 'Algo falló.' } } };

    expect(mensajeDeError(error)).toBe('Algo falló.');
  });

  it('cae al mensaje del error cuando no hay respuesta, por ejemplo sin red', () => {
    expect(mensajeDeError(new Error('Network Error'))).toBe('Network Error');
  });
});

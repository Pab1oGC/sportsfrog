# Reglas de fotografía para credenciales deportivas

**Versión:** 0.1 (borrador)
**Estado:** reglas de negocio aprobadas; umbrales pendientes de calibración.
**Medición:** OFIQ 1.1.2 vía `python-ofiq` 0.3.1 (implementación de referencia de ISO/IEC 29794-5).

Este documento define qué foto sirve para una credencial. Cada regla dice qué mide
OFIQ, qué evidencia hay de que la medición funciona, y qué mensaje ve el usuario
cuando la foto se rechaza.

El veredicto es binario: **aprobada** o **rechazada**. Lo que decide cuál es la
**criticidad** de la regla que falló:

- Una regla **crítica** que falla **rechaza** la foto, y la foto no se guarda.
- Una regla **no crítica** que falla sólo **advierte**: la foto se guarda, bajo la
  responsabilidad de quien la sube, y las advertencias quedan registradas.
- Una regla **no verificada** (sin umbral, o medida que OFIQ no pudo calcular) no
  es un defecto. Se anota aparte y nunca bloquea.

Una regla es crítica cuando se cumplen **las dos** condiciones: la medición es
confiable y el defecto es grave. Medir mal y bloquear es peor que no bloquear:
por eso nitidez y expresión, que funcionan pero con escalas sin calibrar, sólo
advierten.

**Excepción decidida a mano:** la mascarilla (R14) bloquea aunque su medición no
esté calibrada, porque el defecto es grave. Se acepta el costo: en la prueba
disponible, 47 de 120 caras sin mascarilla puntuaban como ocluidas. Bajar el
umbral no lo corrige, porque la medida es de todo o nada; sólo lo corrige
calibrar con retratos reales.

---

## 1. Perfil y alcance

El perfil se deriva de ISO/IEC 29794-5 (calidad de imagen facial), que mide qué tan
conforme es una foto con los requisitos de imagen de pasaporte (ICAO). Con
**desviaciones explícitas**, listadas en la sección 4.

**Fuera de alcance en esta versión:**

- Prendas religiosas que cubren la cabeza (hiyab, kipá, turbante). No aplica en el
  contexto actual.
- Reglas distintas para niños y bebés. Se evaluará más adelante.

---

## 2. Reglas

Cada regla tiene tres columnas de evidencia:

- **Medida OFIQ:** la medida que la implementa.
- **Evidencia:** qué se probó y con qué resultado. "Probado" significa que la medida
  separa la condición con datos reales o de prueba. "Presente" significa que la
  medida existe pero no se probó contra esta condición.
- **Umbral:** pendiente de calibración (ver sección 5).

**Críticas (bloquean):** R1, R2, R12, R13, R14 y R15. **El resto advierte.**
La lista que manda es `criticas` en `config.yaml` del validador; esta es su copia
en prosa.

| # | Regla | Medida OFIQ | Evidencia | Umbral | Mensaje al usuario |
|---|---|---|---|---|---|
| R1 | Exactamente una cara visible | `SingleFacePresent` | Presente | — | "Hay más de una persona en la foto. Subí una foto con una sola persona." |
| R2 | Ojos abiertos | `EyesOpen` | Probado (TONO `ce`, `ceg`) | pendiente | "Los ojos aparecen cerrados. Subí una foto con los ojos abiertos." |
| R3 | Expresión neutra, sin sonrisa | `ExpressionNeutrality` | Probado (TONO `sm`; CelebA: 353/400 sonrientes bajo 50 vs 81/400 neutras). Los falsos positivos mezclan sonrisas leves no etiquetadas y pose de perfil; hace falta etiquetado manual para medir la tasa real | pendiente | "La expresión no es neutra. Subí una foto sin sonrisa." |
| R4 | Boca cerrada | `MouthClosed` | **No separa sonrisa** (CelebA: 12/400 sonrientes y 7/400 neutras bajo 50). Sin prueba de boca abierta todavía | pendiente | "La boca aparece abierta. Subí una foto con la boca cerrada." |
| R5 | Pose frontal | `HeadPoseYaw`, `HeadPosePitch`, `HeadPoseRoll` | Presente | pendiente | "La cara no está de frente. Subí una foto mirando a la cámara." |
| R6 | Cara centrada y tamaño adecuado | `HeadSize`, `LeftwardCropOfTheFaceImage`, `RightwardCropOfTheFaceImage`, `MarginAboveOfTheFaceImage`, `MarginBelowOfTheFaceImage` | Probado parcial (TONO `zoom`) | pendiente | "La cara está muy lejos, muy cerca o descentrada. Encuadrá el rostro en el centro." |
| R7 | Foto nítida | `Sharpness` | Probado (TONO `oof`) | pendiente | "La foto está borrosa. Subí una foto más nítida." |
| R8 | Exposición correcta | `UnderExposurePrevention`, `OverExposurePrevention` | Probado parcial (TONO `expos`, `light`) | pendiente | "La foto está muy oscura o muy clara. Subí una foto con luz pareja." |
| R9 | Iluminación uniforme en la cara | `IlluminationUniformity` | Débil (TONO `light`) | pendiente | "La luz cae desigual sobre la cara. Subí una foto con luz pareja." |
| R10 | Fondo uniforme | `BackgroundUniformity` | Probado (TONO `bkg`) | pendiente | "El fondo es irregular. Subí una foto con un fondo liso." |
| R11 | Color natural | `NaturalColour` | Probado (TONO `sat`) | pendiente | "Los colores de la foto están alterados. Subí una foto con colores naturales." |
| R12 | Sin lentes de sol | `EyesVisible`, `FaceOcclusionPrevention` | Probado (TONO `sun`; CelebA) | pendiente | "Se ven lentes de sol. Subí una foto sin lentes de sol." |
| R13 | Sin gorra ni cabeza cubierta | `NoHeadCoverings` | Probado (TONO `cap`) | pendiente | "La cabeza aparece cubierta. Subí una foto sin gorra." |
| R14 | Sin mascarilla | `MouthOcclusionPrevention` | Probado parcial (100 recortes de calle) | pendiente | "Se ve una mascarilla. Subí una foto sin mascarilla." |
| R15 | Cara detectable | error `FaceDetectionError` de OFIQ | Probado (mascarillas: 25.6% vs 12.4%) | — | "No se pudo encontrar una cara en la foto. Subí una foto donde se vea el rostro completo." |

**Regla que no tiene medida:**

| # | Regla | Decisión | Mensaje |
|---|---|---|---|
| R16 | Lentes transparentes | **Permitidos.** OFIQ no los marca, y no hay medida para distinguirlos de la ausencia de lentes. | — |

**Formato del archivo** (no lo mide OFIQ; se valida antes):

- JPG o PNG.
- Proporción 4:5.
- Mínimo 400 × 500 píxeles.
- El normalizador (`ImageNormalizer`) ya corrige la orientación y elimina los metadatos
  antes de cualquier medición.

---

## 3. Lo que OFIQ no mide y decidimos nosotros

| Tema | Decisión |
|---|---|
| Lentes transparentes | Permitidos (R16). OFIQ no los distingue. |
| Ropa, uniforme, color de vestimenta | Sin restricción. |
| Fondo de color específico | Sin restricción; solo uniforme (R10). |
| Blanco y negro | Permitido. |
| Selfie o distancia de brazo | Sin regla propia; se rechaza si fallan R5 o R6. |
| Foto de una foto (carnet escaneado) | Sin regla propia en esta versión. |
| Hiyab, kipá, turbante | Fuera de alcance (ver sección 1). |
| Niños | Fuera de alcance en esta versión. Se aplican las mismas reglas a todas las edades. |

---

## 4. Desviaciones respecto del pasaporte

Estas son las diferencias deliberadas con el estándar ICAO:

1. **Lentes transparentes permitidos.** ICAO no los admite.
2. **Fondo uniforme de cualquier color.** ICAO exige fondo claro y liso.
3. **Sin requisito de resolución para impresión.** Se exige solo la mínima para la credencial.
4. **Sin requisito de iluminación de estudio.** Se exige solo uniformidad (R9).

---

## 5. Umbrales (pendientes)

Ningún umbral está fijado todavía. Se calibran con un conjunto de retratos reales,
separado del que se usó para explorar las medidas, y se miden una sola vez sobre un
conjunto de prueba aparte.

**Lo que falta para calibrar:**

- Retratos reales con y sin mascarilla (R14). Los recortes de calle usados hasta ahora
  no alcanzan: 39% de las caras sin mascarilla también puntúan bajo en la medida de boca.
- Verificar que `ExpressionNeutrality` y `MouthClosed` separan sonrisa de expresión
  neutra en retratos (hoy solo hay evidencia en TONO).
- Verificar `IlluminationUniformity` (R9): la evidencia actual es débil.

---

## 6. Trazabilidad

- Cada respuesta del validador incluye `version_reglas` (este documento) y la versión
  de OFIQ.
- Un cambio de umbral sube la versión de reglas.
- Las fotos no se guardan después de validarlas; se guardan el veredicto y los valores
  numéricos.

import copy

import pytest

from validador.reglas import MENSAJE_SIN_CARA, REGLAS, cargar_config, evaluar


def medidas_buenas(**cambios):
    """Todas las medidas de OFIQ en 100 (lo que sale de una foto perfecta), con cambios."""
    m = {r.medida: 100.0 for r in REGLAS}
    m.update(cambios)
    return m


def config_todo_calibrado():
    """Config con un corte en cada regla, para probar la rama 'aprobada'."""
    cfg = copy.deepcopy(cargar_config())
    for regla in REGLAS:
        if cfg["umbrales"].get(regla.id) is None:
            cfg["umbrales"][regla.id] = 50
    return cfg


def test_config_tiene_umbral_para_cada_regla():
    cfg = cargar_config()
    faltantes = [r.id for r in REGLAS if r.id not in cfg["umbrales"]]
    assert faltantes == []


def test_config_marca_no_calibrado():
    assert cargar_config()["calibrado"] is False


def test_foto_buena_con_reglas_sin_calibrar_queda_aprobada():
    # Las reglas con umbral null no bloquean ni son defectos: quedan anotadas
    # aparte, como no verificadas.
    r = evaluar(medidas_buenas())
    assert r["estado"] == "aprobada"
    assert r["motivos"] == []
    assert r["advertencias"] == []
    assert "No verificado: boca cerrada." in r["no_verificado"]


def test_foto_buena_con_todo_calibrado_no_deja_nada_sin_verificar():
    r = evaluar(medidas_buenas(), config=config_todo_calibrado())
    assert r["estado"] == "aprobada"
    assert r["advertencias"] == []
    assert r["no_verificado"] == []


def test_regla_sin_calibrar_que_falla_no_rechaza():
    # R4 (boca cerrada) no tiene umbral: aunque la medida sea mala, no bloquea.
    r = evaluar(medidas_buenas(MouthClosed=0.0))
    assert r["estado"] == "aprobada"
    assert r["motivos"] == []


def test_una_regla_critica_que_falla_rechaza_la_foto():
    r = evaluar(medidas_buenas(EyesVisible=2.1))
    assert r["estado"] == "rechazada"
    assert any("lentes de sol" in m for m in r["motivos"])
    assert r["advertencias"] == []


def test_una_regla_no_critica_que_falla_solo_advierte():
    # Nitidez no es critica: la foto se puede guardar bajo responsabilidad.
    r = evaluar(medidas_buenas(Sharpness=5.0))
    assert r["estado"] == "aprobada"
    assert r["motivos"] == []
    assert any("borrosa" in a for a in r["advertencias"])


def test_una_critica_y_una_no_critica_se_reportan_por_separado():
    r = evaluar(medidas_buenas(EyesOpen=10.0, Sharpness=5.0))
    assert r["estado"] == "rechazada"
    assert any("ojos" in m for m in r["motivos"])
    assert any("borrosa" in a for a in r["advertencias"])


def test_cada_regla_declarada_critica_existe():
    # Una regla critica que no existe seria una clasificacion que no se aplica.
    ids = {regla.id for regla in REGLAS}
    assert set(cargar_config()["criticas"]) <= ids


def test_la_mascarilla_es_critica_por_decision_explicita():
    r = evaluar(medidas_buenas(MouthOcclusionPrevention=6.7))
    assert r["estado"] == "rechazada"
    assert any("mascarilla" in m for m in r["motivos"])


def test_ojos_cerrados_rechaza_con_mensaje():
    r = evaluar(medidas_buenas(EyesOpen=10.0))
    assert r["estado"] == "rechazada"
    assert "Los ojos aparecen cerrados" in r["motivos"][0]


def test_lentes_de_sol_rechazan():
    r = evaluar(medidas_buenas(EyesVisible=2.1))
    assert r["estado"] == "rechazada"
    assert any("lentes de sol" in m for m in r["motivos"])


def test_lentes_transparentes_no_rechazan():
    # Decisión R16: OFIQ no marca lentes transparentes, y no se rechazan.
    r = evaluar(medidas_buenas(EyesVisible=90.0))
    assert not any("lentes de sol" in m for m in r["motivos"])


def test_mascarilla_rechaza():
    r = evaluar(medidas_buenas(MouthOcclusionPrevention=6.7))
    assert r["estado"] == "rechazada"
    assert any("mascarilla" in m for m in r["motivos"])


def test_sin_cara_rechaza_con_mensaje_r15():
    r = evaluar(None, cara_detectada=False)
    assert r["estado"] == "rechazada"
    assert r["motivos"] == [MENSAJE_SIN_CARA]


def test_medida_faltante_no_es_un_defecto_sino_algo_sin_verificar():
    m = medidas_buenas()
    m["Sharpness"] = None
    r = evaluar(m, config=config_todo_calibrado())
    assert r["estado"] == "aprobada"
    assert "No verificado: nitidez." in r["no_verificado"]
    assert r["advertencias"] == []


def test_mensaje_repetido_aparece_una_sola_vez():
    # Las tres medidas de pose comparten mensaje: debe salir una vez. La pose
    # no es critica, asi que cae en advertencias.
    cfg = config_todo_calibrado()
    r = evaluar(medidas_buenas(HeadPoseYaw=0, HeadPosePitch=0, HeadPoseRoll=0), config=cfg)
    assert r["advertencias"].count(
        "La cara no está de frente. Subí una foto mirando a la cámara.") == 1


def test_umbral_es_inclusivo():
    cfg = config_todo_calibrado()
    cfg["umbrales"]["R2"] = 50
    assert evaluar(medidas_buenas(EyesOpen=50.0), config=cfg)["estado"] == "aprobada"
    assert evaluar(medidas_buenas(EyesOpen=49.9), config=cfg)["estado"] == "rechazada"


@pytest.mark.parametrize("regla", REGLAS, ids=lambda r: r.id)
def test_cada_regla_puede_fallar_y_cae_donde_corresponde(regla):
    # Cada regla tiene que poder fallar: las criticas rechazan, las demas
    # solo advierten. Ninguna puede quedar muda.
    cfg = config_todo_calibrado()
    r = evaluar(medidas_buenas(**{regla.medida: 0.0}), config=cfg)

    if regla.id in cfg["criticas"]:
        assert r["estado"] == "rechazada", regla.id
        assert regla.mensaje in r["motivos"], regla.id
    else:
        assert r["estado"] == "aprobada", regla.id
        assert regla.mensaje in r["advertencias"], regla.id

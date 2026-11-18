import { useCallback, useMemo, useRef, useState } from 'react';
import { ApiError, criarApiClient } from './api/client';
import type { ApiClient } from './api/client';
import type {
  AreaId,
  Entrevista,
  FeedbackRecomendacao,
  FeedbackRegistrarRequest,
  FormatoExportacao,
  MapaHabilidades,
  PerfilUsuario,
  PlanoDesenvolvimento,
  RecomendacaoArea,
  Sessao,
} from './api/types';
import { ExclusaoDadosConfirmacao } from './components/ExclusaoDadosConfirmacao';
import type { EventoFinal, MensagemChat } from './hooks/useMensagemEnvio';
import { BoasVindas } from './telas/BoasVindas';
import { Conversa } from './telas/Conversa';
import { MapaHabilidadesTela } from './telas/MapaHabilidadesTela';
import { Plano } from './telas/Plano';
import { Recomendacoes } from './telas/Recomendacoes';

export type Tela = 'boasVindas' | 'conversa' | 'recomendacoes' | 'mapa' | 'plano';

export const MSG_INICIAR = 'Não foi possível iniciar a entrevista. Tente novamente.';
export const MSG_ENCERRAR = 'Não foi possível encerrar a entrevista. Tente novamente.';
export const MSG_RECOMENDACOES = 'Não foi possível gerar as recomendações. Tente novamente.';
export const MSG_MAPA = 'Não foi possível carregar o mapa de habilidades. Tente novamente.';
export const MSG_PLANO = 'Não foi possível gerar o plano. Tente novamente.';
export const MSG_EXPORTACAO = 'Não foi possível exportar o plano. Tente novamente.';
export const MSG_RETOMAR = 'Não foi possível retomar a entrevista. Tente novamente.';

const MINIMO_CATEGORIAS_CONFIANCA = 3;

function mensagemDe(e: unknown, padrao: string): string {
  return e instanceof ApiError && e.message ? e.message : padrao;
}

function baixaConfiancaDe(entrevista: Entrevista | undefined): boolean {
  if (!entrevista) return true;
  const respondidas = Object.values(entrevista.categorias).filter((c) => c === 'respondida').length;
  return respondidas < MINIMO_CATEGORIAS_CONFIANCA;
}

function baixarArquivo(blob: Blob, nome: string) {
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = nome;
  document.body.appendChild(a);
  a.click();
  a.remove();
  URL.revokeObjectURL(url);
}

export interface AppProps {
  /** Cliente da API; injetável para testes. */
  api?: ApiClient;
}

interface EstadoPlano {
  areaId: AreaId;
  plano: PlanoDesenvolvimento | null;
  gerando: boolean;
  erro: string | null;
  precisaDisponibilidade: boolean;
  semHabilidadesPendentes: boolean;
  exportando: boolean;
  erroExportacao: string | null;
}

export function App({ api: apiProp }: AppProps = {}) {
  const api = useMemo(() => apiProp ?? criarApiClient(), [apiProp]);

  const [tela, setTela] = useState<Tela>('boasVindas');
  const [sessao, setSessao] = useState<Sessao | null>(null);
  const [conversaKey, setConversaKey] = useState(0);

  const [iniciando, setIniciando] = useState(false);
  const [erroInicio, setErroInicio] = useState<string | null>(null);

  const [encerrando, setEncerrando] = useState(false);
  const [erroEncerrar, setErroEncerrar] = useState<string | null>(null);

  const [recomendacoes, setRecomendacoes] = useState<RecomendacaoArea[]>([]);
  const [feedbacks, setFeedbacks] = useState<FeedbackRecomendacao[]>([]);
  const [carregandoRec, setCarregandoRec] = useState(false);
  const [erroRec, setErroRec] = useState<string | null>(null);

  const [areaMapa, setAreaMapa] = useState<AreaId | null>(null);
  const [mapa, setMapa] = useState<MapaHabilidades | null>(null);
  const [carregandoMapa, setCarregandoMapa] = useState(false);
  const [erroMapa, setErroMapa] = useState<string | null>(null);
  const [nomesHabilidades, setNomesHabilidades] = useState<Record<string, string>>({});

  const [estadoPlano, setEstadoPlano] = useState<EstadoPlano | null>(null);
  const ultimasHoras = useRef<number | undefined>(undefined);
  const ultimoFormato = useRef<FormatoExportacao>('pdf');

  const entrevista = sessao?.entrevista;
  const perfil = sessao?.perfil;

  const nomeDaArea = useCallback(
    (areaId: AreaId) => recomendacoes.find((r) => r.areaId === areaId)?.nome ?? areaId,
    [recomendacoes],
  );

  // --- Início e conversa -------------------------------------------------------------------

  const iniciar = useCallback(async () => {
    setIniciando(true);
    setErroInicio(null);
    try {
      const nova = await api.criarSessao();
      setSessao(nova);
      setFeedbacks(nova.feedbacks ?? []);
      setConversaKey((k) => k + 1);
      setTela('conversa');
    } catch (e) {
      setErroInicio(mensagemDe(e, MSG_INICIAR));
    } finally {
      setIniciando(false);
    }
  }, [api]);

  const carregarRecomendacoes = useCallback(async () => {
    setTela('recomendacoes');
    setCarregandoRec(true);
    setErroRec(null);
    try {
      const lista = await api.listarRecomendacoes();
      setRecomendacoes(lista);
      try {
        const atual = await api.obterSessao();
        setSessao(atual);
        setFeedbacks(atual.feedbacks ?? []);
      } catch {
        // A lista já foi obtida; a confiança é calculada com a entrevista local.
      }
    } catch (e) {
      setErroRec(mensagemDe(e, MSG_RECOMENDACOES));
    } finally {
      setCarregandoRec(false);
    }
  }, [api]);

  const aoFinalizarMensagem = useCallback(
    (evento: EventoFinal) => {
      const novaEntrevista = evento.entrevista;
      const novoPerfil: PerfilUsuario | undefined = evento.perfil;
      setSessao((s) =>
        s ? { ...s, entrevista: novaEntrevista, perfil: novoPerfil ?? s.perfil } : s,
      );
      if (novaEntrevista.estado === 'concluida') void carregarRecomendacoes();
    },
    [carregarRecomendacoes],
  );

  const encerrar = useCallback(async () => {
    setEncerrando(true);
    setErroEncerrar(null);
    try {
      await api.encerrarEntrevista();
    } catch (e) {
      // Sem nenhuma categoria respondida a API recusa o encerramento (Req. 2.7).
      setErroEncerrar(mensagemDe(e, MSG_ENCERRAR));
      setEncerrando(false);
      return;
    }
    setEncerrando(false);
    await carregarRecomendacoes();
  }, [api, carregarRecomendacoes]);

  const continuarEntrevista = useCallback(async () => {
    try {
      const atual = await api.obterSessao();
      setSessao(atual);
      setConversaKey((k) => k + 1);
      setErroEncerrar(null);
      setTela('conversa');
    } catch (e) {
      setErroRec(mensagemDe(e, MSG_RETOMAR));
    }
  }, [api]);

  const historicoInicial = useMemo<MensagemChat[]>(
    () =>
      (sessao?.historico ?? []).map((m) => ({
        clientMessageId: m.clientMessageId,
        papel: m.papel,
        texto: m.texto,
      })),
    // Recalculado só quando a conversa é (re)montada.
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [conversaKey],
  );

  // --- Feedback ----------------------------------------------------------------------------

  const registrarFeedback = useCallback(
    async (areaId: AreaId, req: FeedbackRegistrarRequest) => {
      const feedback = await api.registrarFeedback(areaId, req);
      setFeedbacks((atuais) => [...atuais.filter((f) => f.areaId !== areaId), feedback]);
    },
    [api],
  );

  // --- Mapa --------------------------------------------------------------------------------

  const carregarMapa = useCallback(
    async (areaId: AreaId) => {
      setAreaMapa(areaId);
      setMapa(null);
      setErroMapa(null);
      setCarregandoMapa(true);
      setTela('mapa');
      try {
        const resultado = await api.obterMapaHabilidades(areaId);
        setMapa(resultado);
        setNomesHabilidades((n) => ({
          ...n,
          ...Object.fromEntries(resultado.habilidades.map((h) => [h.id, h.nome])),
        }));
      } catch (e) {
        setErroMapa(mensagemDe(e, MSG_MAPA));
      } finally {
        setCarregandoMapa(false);
      }
    },
    [api],
  );

  // --- Plano -------------------------------------------------------------------------------

  const gerarPlano = useCallback(
    async (areaId: AreaId, horas?: number) => {
      ultimasHoras.current = horas;
      setTela('plano');
      const base: EstadoPlano = {
        areaId,
        plano: null,
        gerando: false,
        erro: null,
        precisaDisponibilidade: false,
        semHabilidadesPendentes: false,
        exportando: false,
        erroExportacao: null,
      };

      // Sem disponibilidade no perfil nem informada agora: pede antes de chamar a API (Req. 5.6).
      if (horas === undefined && perfil?.horasEstudoPorSemana == null) {
        setEstadoPlano({ ...base, precisaDisponibilidade: true });
        return;
      }

      setEstadoPlano({ ...base, gerando: true });

      // Nomes das habilidades para exibição; falha aqui não impede o plano.
      if (!Object.keys(nomesHabilidades).length) {
        try {
          const m = await api.obterMapaHabilidades(areaId);
          setNomesHabilidades(Object.fromEntries(m.habilidades.map((h) => [h.id, h.nome])));
        } catch {
          /* exibe ids */
        }
      }

      try {
        const plano = await api.gerarPlano(areaId, horas);
        setEstadoPlano({ ...base, plano });
      } catch (e) {
        if (e instanceof ApiError) {
          const campos = Object.keys(e.problem?.errors ?? {});
          if (e.status === 400 && campos.some((c) => /horas|disponibilidade/i.test(c))) {
            setEstadoPlano({ ...base, precisaDisponibilidade: true });
            return;
          }
          if (e.status === 422 && e.problem?.type?.endsWith('PlanoSemHabilidadesPendentes')) {
            setEstadoPlano({ ...base, semHabilidadesPendentes: true });
            return;
          }
        }
        setEstadoPlano({ ...base, erro: mensagemDe(e, MSG_PLANO) });
      }
    },
    [api, perfil, nomesHabilidades],
  );

  const exportar = useCallback(
    async (formato: FormatoExportacao) => {
      const plano = estadoPlano?.plano;
      if (!plano) return;
      setEstadoPlano((s) => (s ? { ...s, exportando: true, erroExportacao: null } : s));
      try {
        const blob = await api.exportarPlano(plano.id, formato);
        baixarArquivo(blob, `plano-${plano.areaId}.${formato}`);
        setEstadoPlano((s) => (s ? { ...s, exportando: false } : s));
      } catch (e) {
        setEstadoPlano((s) =>
          s ? { ...s, exportando: false, erroExportacao: mensagemDe(e, MSG_EXPORTACAO) } : s,
        );
      }
    },
    [api, estadoPlano],
  );

  // --- Exclusão ----------------------------------------------------------------------------

  const excluirDados = useCallback(async () => {
    await api.excluirSessao();
    setSessao(null);
    setRecomendacoes([]);
    setFeedbacks([]);
    setMapa(null);
    setAreaMapa(null);
    setNomesHabilidades({});
    setEstadoPlano(null);
    setErroEncerrar(null);
    setErroRec(null);
    setErroInicio(null);
    setTela('boasVindas');
  }, [api]);

  // --- Renderização ------------------------------------------------------------------------

  return (
    <main>
      <h1>IA Mentor de Carreira</h1>

      {tela === 'boasVindas' && (
        <>
          <BoasVindas onIniciar={() => void iniciar()} iniciando={iniciando} />
          {erroInicio && (
            <div role="alert">
              <p>{erroInicio}</p>
              <button type="button" onClick={() => void iniciar()} disabled={iniciando}>
                Tentar novamente
              </button>
            </div>
          )}
        </>
      )}

      {tela === 'conversa' && entrevista && (
        <>
          <Conversa
            key={conversaKey}
            entrevista={entrevista}
            enviarMensagem={(req, signal) => api.enviarMensagem(req, signal)}
            historicoInicial={historicoInicial}
            onFinal={aoFinalizarMensagem}
          />
          <button type="button" onClick={() => void encerrar()} disabled={encerrando}>
            Encerrar entrevista e ver recomendações
          </button>
          {encerrando && <p role="status">Encerrando a entrevista…</p>}
          {erroEncerrar && (
            <div role="alert">
              <p>{erroEncerrar}</p>
            </div>
          )}
        </>
      )}

      {tela === 'recomendacoes' && (
        <>
          {carregandoRec ? (
            <p role="status">Gerando recomendações…</p>
          ) : (
            <Recomendacoes
              recomendacoes={recomendacoes}
              feedbacks={feedbacks}
              baixaConfianca={baixaConfiancaDe(entrevista)}
              onContinuarEntrevista={
                entrevista?.estado === 'emAndamento' ? () => void continuarEntrevista() : undefined
              }
              erro={erroRec}
              onTentarNovamente={() => void carregarRecomendacoes()}
              onFeedback={registrarFeedback}
              onVerMapa={(areaId) => void carregarMapa(areaId)}
              onGerarPlano={(areaId) => void gerarPlano(areaId)}
            />
          )}
        </>
      )}

      {tela === 'mapa' && areaMapa && (
        <MapaHabilidadesTela
          mapa={mapa}
          nomeArea={nomeDaArea(areaMapa)}
          carregando={carregandoMapa}
          erro={erroMapa}
          onTentarNovamente={() => void carregarMapa(areaMapa)}
          onGerarPlano={(areaId) => void gerarPlano(areaId)}
          onVoltar={() => setTela('recomendacoes')}
        />
      )}

      {tela === 'plano' && estadoPlano && (
        <Plano
          plano={estadoPlano.plano}
          nomesHabilidades={nomesHabilidades}
          precisaDisponibilidade={estadoPlano.precisaDisponibilidade}
          onInformarDisponibilidade={(h) => void gerarPlano(estadoPlano.areaId, h)}
          semHabilidadesPendentes={estadoPlano.semHabilidadesPendentes}
          gerando={estadoPlano.gerando}
          erro={estadoPlano.erro}
          onTentarNovamente={() => void gerarPlano(estadoPlano.areaId, ultimasHoras.current)}
          onVoltar={() => setTela('recomendacoes')}
          onExportar={(f) => {
            ultimoFormato.current = f;
            void exportar(f);
          }}
          exportando={estadoPlano.exportando}
          erroExportacao={estadoPlano.erroExportacao}
          onTentarExportarNovamente={() => void exportar(ultimoFormato.current)}
        />
      )}

      {tela !== 'boasVindas' && (
        <footer>
          <ExclusaoDadosConfirmacao onConfirmar={excluirDados} />
        </footer>
      )}
    </main>
  );
}

import { useId, useState } from 'react';

export interface ExclusaoDadosConfirmacaoProps {
  /** Executa a exclusão; deve rejeitar em caso de falha. */
  onConfirmar: () => Promise<void>;
}

/** Exclusão de dados com confirmação explícita (Req. 7.3, 7.4). */
export function ExclusaoDadosConfirmacao({ onConfirmar }: ExclusaoDadosConfirmacaoProps) {
  const uid = useId();
  const [confirmando, setConfirmando] = useState(false);
  const [excluindo, setExcluindo] = useState(false);
  const [erro, setErro] = useState<string | null>(null);

  async function confirmar() {
    setExcluindo(true);
    setErro(null);
    try {
      await onConfirmar();
      setConfirmando(false);
    } catch {
      setErro('Não foi possível excluir seus dados. Tente novamente.');
    } finally {
      setExcluindo(false);
    }
  }

  if (!confirmando) {
    return (
      <button type="button" onClick={() => setConfirmando(true)}>
        Excluir meus dados
      </button>
    );
  }

  return (
    <div role="alertdialog" aria-modal="false" aria-labelledby={`${uid}-t`} aria-describedby={`${uid}-d`}>
      <h3 id={`${uid}-t`}>Excluir meus dados?</h3>
      <p id={`${uid}-d`}>
        Seu perfil, histórico, recomendações, planos e feedbacks serão removidos e não poderão ser
        recuperados.
      </p>
      <button type="button" onClick={() => void confirmar()} disabled={excluindo}>
        Confirmar exclusão
      </button>
      <button type="button" onClick={() => setConfirmando(false)} disabled={excluindo}>
        Cancelar
      </button>
      {erro && <p role="alert">{erro}</p>}
    </div>
  );
}

import { useId, useState } from 'react';
import type { FormEvent } from 'react';
import { LIMITES } from '../api/types';

export interface DisponibilidadeFormProps {
  onSubmit: (horasPorSemana: number) => void;
  desabilitado?: boolean;
}

/** Solicita a disponibilidade semanal (1 a 168 h) antes de gerar o plano (Req. 5.6). */
export function DisponibilidadeForm({ onSubmit, desabilitado }: DisponibilidadeFormProps) {
  const uid = useId();
  const [valor, setValor] = useState('');
  const [erro, setErro] = useState<string | null>(null);

  function aoSubmeter(e: FormEvent) {
    e.preventDefault();
    const n = Number(valor);
    if (
      valor.trim() === '' ||
      !Number.isInteger(n) ||
      n < LIMITES.horasPorSemanaMin ||
      n > LIMITES.horasPorSemanaMax
    ) {
      setErro(
        `Informe um número inteiro de ${LIMITES.horasPorSemanaMin} a ${LIMITES.horasPorSemanaMax} horas por semana.`,
      );
      return;
    }
    setErro(null);
    onSubmit(n);
  }

  return (
    <form onSubmit={aoSubmeter} noValidate>
      <label htmlFor={`${uid}-horas`}>Horas de estudo por semana</label>
      <input
        id={`${uid}-horas`}
        type="number"
        inputMode="numeric"
        value={valor}
        onChange={(e) => setValor(e.target.value)}
        aria-invalid={erro !== null}
        aria-describedby={erro ? `${uid}-erro` : undefined}
      />
      {erro && (
        <p id={`${uid}-erro`} role="alert">
          {erro}
        </p>
      )}
      <button type="submit" disabled={desabilitado}>
        Gerar plano
      </button>
    </form>
  );
}

import type { Entrevista } from '../api/types';
import { ChatPanel } from '../components/ChatPanel';
import type { ChatPanelProps } from '../components/ChatPanel';
import { ProgressoEntrevista } from '../components/ProgressoEntrevista';

export interface ConversaProps extends ChatPanelProps {
  entrevista: Entrevista;
}

export function Conversa({ entrevista, ...chat }: ConversaProps) {
  return (
    <section aria-labelledby="conversa-titulo">
      <h2 id="conversa-titulo">Conversa</h2>
      <ProgressoEntrevista entrevista={entrevista} />
      <ChatPanel {...chat} />
    </section>
  );
}

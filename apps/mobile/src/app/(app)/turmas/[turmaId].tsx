import { useLocalSearchParams } from "expo-router";

import { TurmaScreen } from "../../../professor/TurmaScreen";

export default function Turma() {
  const { turmaId } = useLocalSearchParams<{ turmaId: string }>();
  return <TurmaScreen turmaId={turmaId} />;
}

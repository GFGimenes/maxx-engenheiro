import type { ReactNode } from "react";
import { StyleSheet, Text, View } from "react-native";

import { textStyles } from "../theme/fonts";
import { useTheme } from "../theme/ThemeProvider";
import { Button } from "./Button";
import { Icon, type IconName } from "./Icon";

const TAMANHO_DO_ICONE = 32;

interface EmptyStateProps {
  title: string;
  message?: string;
  icon?: IconName;
  // Uma saída para quem chegou aqui, quando houver.
  action?: { label: string; onPress: () => void };
}

export function EmptyState({
  title,
  message,
  icon = "inbox",
  action,
}: EmptyStateProps) {
  const { colors } = useTheme();

  return (
    <StateLayout
      icon={<Icon name={icon} color={colors.ink2} size={TAMANHO_DO_ICONE} />}
      title={title}
      message={message}
      action={action}
    />
  );
}

interface ErrorStateProps {
  title?: string;
  message?: string;
  // Sem isso, a tela só informa. Com isso, oferece tentar de novo.
  onRetry?: () => void;
}

// Todo erro em tela vira isto: mensagem amigável, nunca o erro cru. `like` só no ícone.
export function ErrorState({
  title = "Não deu certo agora.",
  message = "Tente de novo em instantes.",
  onRetry,
}: ErrorStateProps) {
  const { colors } = useTheme();

  return (
    <StateLayout
      role="alert"
      icon={<Icon name="alert" color={colors.like} size={TAMANHO_DO_ICONE} />}
      title={title}
      message={message}
      action={
        onRetry ? { label: "Tentar de novo", onPress: onRetry } : undefined
      }
    />
  );
}

function StateLayout({
  role,
  icon,
  title,
  message,
  action,
}: {
  role?: "alert";
  icon: ReactNode;
  title: string;
  message?: string;
  action?: { label: string; onPress: () => void };
}) {
  const { colors } = useTheme();

  return (
    <View style={styles.coluna}>
      {icon}
      {/* O anúncio fica só no texto: envolvendo o botão, ele o esconderia do leitor de tela. */}
      <View accessible={!!role} role={role} style={styles.textos}>
        <Text style={[styles.centro, textStyles.secao, { color: colors.ink }]}>
          {title}
        </Text>
        {message && (
          <Text
            style={[styles.centro, textStyles.corpo, { color: colors.ink2 }]}
          >
            {message}
          </Text>
        )}
      </View>
      {action && (
        <Button
          label={action.label}
          variant="secondary"
          onPress={action.onPress}
        />
      )}
    </View>
  );
}

const styles = StyleSheet.create({
  coluna: {
    alignItems: "center",
    paddingVertical: 32,
    paddingHorizontal: 16,
    gap: 12,
  },
  textos: { alignItems: "center", gap: 4 },
  centro: { textAlign: "center" },
});

namespace Maxx.Domain.Catalogo;

public enum TipoDeItem
{
    Medalha,
    PremioParticipacao,
    Conquista,
}

// Medalha usa bimestre ou semestre; prêmio de participação, o evento; conquista,
// bimestre, semestre, ano ou curso.
public enum Periodo
{
    Bimestre,
    Semestre,
    Ano,
    Curso,
    Evento,
}

public enum BaseDaMedalha
{
    Nota,
    Presenca,
}

public enum FormaDoEvento
{
    Pontual,
    Periodo,
}

public enum Gatilho
{
    PorFato,
    FechamentoDePeriodo,
}

public enum CampoDeDeduplicacao
{
    Aluno,
    Item,
    Materia,
    Bimestre,
    Semestre,
    Ocorrencia,
    Evento,
    DataDoEvento,
}

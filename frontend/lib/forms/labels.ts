/**
 * Polish labels for the form contract's enums, on the model of
 * app/competitions/labels.ts: a full Record per type, so a value added to the
 * wire contract without a label here fails to compile instead of falling
 * through to a blank spot in the creator.
 */
import type {
  AllowedFileFormat,
  CalculationKind,
  FormFieldType,
  LimitKind,
} from "./document-types";
import { COMPETITION_LIMIT_BASES } from "./document-types";

export const FIELD_TYPE_LABELS: Record<FormFieldType, string> = {
  shortText: "Tekst krótki",
  longText: "Tekst długi",
  number: "Liczba",
  amount: "Kwota",
  percent: "Procent",
  date: "Data",
  dateTime: "Data i godzina",
  yesNo: "Tak albo nie",
  singleChoice: "Wybór jednej opcji",
  multipleChoice: "Wybór wielu opcji",
  repeatableTable: "Tabela o zmiennej liczbie wierszy",
  fixedTable: "Tabela o stałej liczbie wierszy",
  file: "Plik",
  statement: "Oświadczenie",
  calculated: "Pole wyliczane",
};

export const ALLOWED_FILE_FORMAT_LABELS: Record<AllowedFileFormat, string> = {
  pdf: "PDF",
  doc: "DOC",
  docx: "DOCX",
  xls: "XLS",
  xlsx: "XLSX",
  jpg: "JPG",
  odt: "ODT",
  ods: "ODS",
};

export const CALCULATION_KIND_LABELS: Record<CalculationKind, string> = {
  sum: "Suma kolumny tabeli",
  product: "Iloczyn składników",
  ratio: "Iloraz jako procent",
  difference: "Różnica składników",
};

export const LIMIT_KIND_LABELS: Record<LimitKind, string> = {
  maxAmount: "Nie więcej niż kwota",
  maxPercentOf: "Nie więcej niż procent",
};

export const COMPETITION_BASIS_LABELS: Record<
  (typeof COMPETITION_LIMIT_BASES)[number],
  string
> = {
  maxGrantAmount: "Maksymalna dotacja na wniosek (ustawienie konkursu)",
  minGrantAmount: "Minimalna dotacja na wniosek (ustawienie konkursu)",
  totalPoolAmount: "Całkowita kwota na realizację zadań (ustawienie konkursu)",
  maxIndirectCostPercent: "Maksymalny procent kosztów pośrednich (ustawienie konkursu)",
  maxInstitutionalDevelopmentPercent:
    "Maksymalny procent rozwoju instytucjonalnego (ustawienie konkursu)",
  maxAverageAnnualRevenue: "Próg średniego rocznego przychodu (ustawienie konkursu)",
};

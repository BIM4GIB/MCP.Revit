export interface RevitElement {
  id: number;
  uniqueId: string;
  category: string;
  familyName: string;
  typeName: string;
  parameters: Record<string, string | number | boolean | null>;
  levelName?: string;
  location?: { x: number; y: number; z: number };
}

export interface ModelInfo {
  title: string;
  filePath: string;
  revitVersion: string;
  elementCount: number;
  projectName: string;
  projectNumber: string;
  projectStatus: string;
  buildingName: string;
  author: string;
  organizationName: string;
}

export interface ScriptResult {
  success: boolean;
  output: string;
  errors: string[];
  executionTimeMs: number;
}

export interface SaveAsResult {
  success: boolean;
  newFilePath: string;
  originalFilePath: string;
  message: string;
}

export interface ElementQuery {
  category?: string;
  familyName?: string;
  typeName?: string;
  levelName?: string;
  parameterFilters?: ParameterFilter[];
  limit?: number;
}

export interface ParameterFilter {
  parameterName: string;
  operator: "equals" | "contains" | "startsWith" | "greaterThan" | "lessThan";
  value: string | number;
}

export interface NamingContext {
  originalName: string;
  revitVersion: string;
  projectNumber: string;
  projectName: string;
  buildingName: string;
  suffix?: string;
  counterSeed?: number;
}

export function revitError(err: unknown): string {
  if (err instanceof Error) {
    const axiosErr = err as { response?: { data?: { message?: string } } };
    return axiosErr.response?.data?.message ?? err.message;
  }
  return String(err);
}

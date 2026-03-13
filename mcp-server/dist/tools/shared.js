export function revitError(err) {
    if (err instanceof Error) {
        const axiosErr = err;
        return axiosErr.response?.data?.message ?? err.message;
    }
    return String(err);
}
//# sourceMappingURL=shared.js.map
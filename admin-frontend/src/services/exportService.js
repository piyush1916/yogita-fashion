import apiClient from "../api/axios";

function triggerDownload(blob, fileName) {
  const url = URL.createObjectURL(blob);
  const link = document.createElement("a");
  link.href = url;
  link.download = fileName;
  document.body.appendChild(link);
  link.click();
  document.body.removeChild(link);
  URL.revokeObjectURL(url);
}

async function downloadCsv(endpoint, fileName) {
  const response = await apiClient.get(endpoint, { responseType: "blob" });
  const blob = new Blob([response.data], { type: "text/csv;charset=utf-8;" });
  triggerDownload(blob, fileName);
}

export { downloadCsv };

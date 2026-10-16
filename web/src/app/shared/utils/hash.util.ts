/** SHA-256 em hexadecimal minúsculo, como a API e o player calculam. */
export async function calcularSha256(arquivo: Blob): Promise<string> {
  const digest = await crypto.subtle.digest('SHA-256', await arquivo.arrayBuffer());
  return Array.from(new Uint8Array(digest), b => b.toString(16).padStart(2, '0')).join('');
}

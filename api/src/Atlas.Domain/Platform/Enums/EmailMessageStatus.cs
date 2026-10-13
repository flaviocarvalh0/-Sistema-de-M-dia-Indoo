namespace Atlas.Domain.Platform.Enums
{
    /// <summary>
    /// Define os possíveis estados de uma mensagem de e-mail dentro do fluxo de envio da plataforma.
    /// </summary>
    public enum EmailMessageStatus
    {
        /// <summary>
        /// A mensagem foi criada, mas ainda não teve seu envio processado.
        /// </summary>
        Pending = 1,

        /// <summary>
        /// A mensagem foi enviada com sucesso pelo servidor SMTP configurado.
        /// </summary>
        Sent = 2,

        /// <summary>
        /// A mensagem falhou durante uma tentativa de envio.
        /// </summary>
        Failed = 3,

        /// <summary>
        /// A mensagem foi cancelada e não deverá ser enviada.
        /// </summary>
        Canceled = 4
    }
}
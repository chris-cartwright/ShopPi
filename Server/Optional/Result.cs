using System;

namespace ShopPi.Optional
{
    public record Result<TOk, TError>
    {
        public record Ok(TOk Value): Result<TOk, TError>;
        public record Error(TError Value): Result<TOk, TError>;

        private Result() {}
    }
}
using System.Net;

namespace IGSB
{
    public class IGResponse<T>
    {
        public HttpStatusCode StatusCode { get; set; }

        public T Response { get; set; }

        public Security Authentication { get; set; }

        public static implicit operator bool(IGResponse<T> inst)
        {
            return inst.StatusCode == HttpStatusCode.OK;
        }

        public static implicit operator HttpStatusCode(IGResponse<T> inst)
        {
            return inst.StatusCode;
        }

        public static implicit operator T(IGResponse<T> inst)
        {
            return inst.Response;
        }
    }
}

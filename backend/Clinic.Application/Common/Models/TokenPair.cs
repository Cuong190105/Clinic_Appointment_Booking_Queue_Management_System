using System;
using System.Collections.Generic;
using System.Text;

namespace Clinic.Application.Common.Models
{
    public record TokenPair(
        string AccessToken,
        string RefreshToken
    );
}

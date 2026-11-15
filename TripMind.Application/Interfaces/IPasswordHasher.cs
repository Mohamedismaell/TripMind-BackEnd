using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TripMind.Application.Interfaces
{
    public interface IPasswordHasher
    {
        string Hash(string plaintext);
        bool Verify(string plaintext, string hash);
    }
}

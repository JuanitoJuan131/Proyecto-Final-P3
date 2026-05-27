using System.Collections.Generic;

namespace DAL.Repositories
{
    public interface IRepositorio<T>
    {
        int Insertar(T entidad);
        void Actualizar(T entidad);
        void Eliminar(int id);
        T ObtenerPorId(int id);
        List<T> ObtenerTodos();
    }
}

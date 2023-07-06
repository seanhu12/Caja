using System;
using System.Collections.Generic;
using System.Text;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Newtonsoft.Json;

namespace Caja
{
    class Program
    {
        static void Main(string[] args)
        {
            var factory = new ConnectionFactory() { HostName = "localhost" }; // Cambia el valor si RabbitMQ no se encuentra en localhost
            using (var connection = factory.CreateConnection())
            using (var channel = connection.CreateModel())
            {
                channel.QueueDeclare(queue: "productos_disponibles",
                                     durable: false,
                                     exclusive: false,
                                     autoDelete: false,
                                     arguments: null);

                channel.QueueDeclare(queue: "productos_seleccionados",
                                     durable: false,
                                     exclusive: false,
                                     autoDelete: false,
                                     arguments: null);

                var productosDisponiblesConsumer = new EventingBasicConsumer(channel);
                productosDisponiblesConsumer.Received += (model, ea) =>
                {
                    var message = Encoding.UTF8.GetString(ea.Body.ToArray());
                    var productosDisponibles = JsonConvert.DeserializeObject<List<Producto>>(message);
                    Console.WriteLine("Listado de productos disponibles recibido:");
                    foreach (var producto in productosDisponibles)
                    {
                        Console.WriteLine($"Nombre: {producto.Nombre}, Precio: {producto.Precio}");
                    }

                    

                    // Simulamos la selección de productos
                    var productosSeleccionados = new List<Producto>();

                    while (true)
                    {
                        Console.WriteLine("Selecciona los productos que deseas (escribe 'terminar' para finalizar la selección):");
                        var seleccion = Console.ReadLine();
                        if (seleccion.ToLower() == "terminar")
                            Console.WriteLine("Gracias por su Compra");
                            break;

                        var productoSeleccionado = productosDisponibles.Find(p => p.Nombre == seleccion);
                        if (productoSeleccionado != null)
                        {
                            productosSeleccionados.Add(productoSeleccionado);
                            Console.WriteLine("Su lista de compras es: ");
                            foreach (var producto in productosSeleccionados)
                            {
                                Console.WriteLine($"Nombre: {producto.Nombre}, Precio: {producto.Precio}");
                                
                            }
                        }
                        else
                        {
                            Console.WriteLine("Producto no disponible. Inténtalo de nuevo.");
                        }
                    }

                    var productosSeleccionadosMessage = JsonConvert.SerializeObject(productosSeleccionados);
                    var body = Encoding.UTF8.GetBytes(productosSeleccionadosMessage);

                    channel.BasicPublish(exchange: "",
                                         routingKey: "productos_seleccionados",
                                         basicProperties: null,
                                         body: body);

                    Console.WriteLine("Productos seleccionados enviados a Inventario.");
                };

                channel.BasicConsume(queue: "productos_disponibles",
                                     autoAck: true,
                                     consumer: productosDisponiblesConsumer);

                Console.WriteLine("Presiona Enter para cerrar la aplicación...");

                // Agregar un bucle para mantener viva la aplicación hasta que se presione una tecla diferente de Enter
                while (Console.ReadKey().Key != ConsoleKey.Escape) { }
            }
        }
    }

    class Producto
    {
        public string Nombre { get; set; }
        public int Precio { get; set; }
    }
}